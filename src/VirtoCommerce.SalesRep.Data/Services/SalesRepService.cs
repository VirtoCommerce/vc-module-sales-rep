using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepService : ISalesRepService
{
    private readonly IMemberService _memberService;
    private readonly IUserSearchService _userSearchService;
    private readonly IOrganizationMembershipService _membershipService;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly Func<UserManager<ApplicationUser>> _userManagerFactory;

    public SalesRepService(
        IMemberService memberService,
        IUserSearchService userSearchService,
        IOrganizationMembershipService membershipService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver,
        Func<UserManager<ApplicationUser>> userManagerFactory)
    {
        _memberService = memberService;
        _userSearchService = userSearchService;
        _membershipService = membershipService;
        _membershipSearchService = membershipSearchService;
        _roleResolver = roleResolver;
        _userManagerFactory = userManagerFactory;
    }

    public virtual async Task<SalesRepDetails> GetByIdAsync(string id)
    {
        if (await _memberService.GetByIdAsync(id, MemberResponseGroup.Full.ToString()) is not Contact contact)
        {
            return null;
        }

        var user = await FindUserByMemberIdAsync(contact.Id);
        var result = ToSalesRep(contact, user);

        if (user != null)
        {
            // The Email table row order isn't guaranteed; the blade treats emails[0] as the login, so put
            // the account's login email first (the rest are "additional emails").
            var loginEmail = !string.IsNullOrEmpty(user.Email) ? user.Email : user.UserName;
            if (!string.IsNullOrEmpty(loginEmail))
            {
                result.Emails =
                [
                    loginEmail,
                    .. result.Emails.Where(e => !string.Equals(e, loginEmail, StringComparison.OrdinalIgnoreCase)),
                ];
            }

            var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();

            var globalRole = user.Roles?.FirstOrDefault(r => grantingRoleIds.Contains(r.Id));
            result.HasGlobalSalesRepRole = globalRole != null;
            if (globalRole != null)
            {
                result.RoleId = globalRole.Id;
                result.RoleName = globalRole.Name;
            }

            var memberships = await GetSalesRepMembershipsAsync(user.Id, grantingRoleIds);
            result.Organizations = memberships
                .Select(m => new SalesRepOrganization
                {
                    OrganizationId = m.OrganizationId,
                    OrganizationName = m.OrganizationName,
                    MembershipId = m.Id,
                })
                .ToList();

            // No global role (per-org-only rep) — derive the role from a membership.
            if (string.IsNullOrEmpty(result.RoleId))
            {
                var membershipRole = memberships
                    .SelectMany(m => m.Roles)
                    .FirstOrDefault(r => grantingRoleIds.Contains(r.RoleId));
                if (membershipRole != null)
                {
                    result.RoleId = membershipRole.RoleId;
                    result.RoleName = membershipRole.RoleName;
                }
            }
        }

        return result;
    }

    public virtual Task<SalesRepDetails> SaveChangesAsync(SalesRepDetails salesRep)
    {
        ArgumentNullException.ThrowIfNull(salesRep);
        return SaveChangesInternalAsync(salesRep);
    }

    protected virtual async Task<SalesRepDetails> SaveChangesInternalAsync(SalesRepDetails salesRep)
    {
        var isNew = string.IsNullOrEmpty(salesRep.Id);

        if (isNew)
        {
            // A login account is mandatory for a Sales Rep. Without a login email (or an explicit user name)
            // account creation fails with an opaque Identity error AFTER the contact was already saved, so
            // reject early with a clear message instead.
            var hasLogin = !string.IsNullOrWhiteSpace(salesRep.UserName)
                || salesRep.Emails?.Any(e => !string.IsNullOrWhiteSpace(e)) == true;
            if (!hasLogin)
            {
                throw new InvalidOperationException("A Sales Rep requires a login email (or user name).");
            }
        }

        var contact = isNew
            ? AbstractTypeFactory<Contact>.TryCreateInstance()
            : await _memberService.GetByIdAsync(salesRep.Id, MemberResponseGroup.Full.ToString()) as Contact
              ?? throw new InvalidOperationException($"Sales Rep '{salesRep.Id}' not found");

        ApplyProfile(contact, salesRep);
        await _memberService.SaveChangesAsync([contact]);
        salesRep.Id = contact.Id;

        try
        {
            // Resolve the granting-role set once and derive both the id-set and the role to assign from it
            // (the UI-chosen role if it grants the permission, else the lazily seeded default).
            var grantingRoles = await _roleResolver.GetRolesGrantingAccessAsync();
            var assignableRole = grantingRoles.FirstOrDefault(r => r.Id == salesRep.RoleId)
                ?? await _roleResolver.EnsureSalesRepRoleAsync();
            var grantingRoleIds = grantingRoles.Select(r => r.Id).Append(assignableRole.Id).ToHashSet();

            using var userManager = _userManagerFactory();

            var user = isNew
                ? await CreateAccountAsync(userManager, contact, salesRep, assignableRole)
                : await UpdateAccountAsync(userManager, contact, salesRep, assignableRole, grantingRoleIds);

            if (user != null)
            {
                await SyncMembershipsAsync(user.Id, salesRep, assignableRole, grantingRoleIds);
            }
        }
        catch when (isNew)
        {
            // The contact was persisted before the account/membership step failed. There is no cross-service
            // transaction, so compensate: roll the just-created contact back (reusing the module's own delete,
            // which also removes any partially-created account) so a failed create never leaves an orphan
            // member. The original exception is rethrown to the caller.
            await TryRollbackContactAsync(contact.Id);
            throw;
        }

        return await GetByIdAsync(salesRep.Id);
    }

    /// <summary>Best-effort rollback of a contact (and its account) after a failed create. Cleanup errors are
    /// swallowed so the caller can rethrow the original failure that triggered the rollback.</summary>
    protected virtual async Task TryRollbackContactAsync(string memberId)
    {
        try
        {
            await DeleteAsync([memberId]);
        }
        catch (Exception)
        {
            // Intentionally ignored — see summary above.
        }
    }

    public virtual async Task DeleteAsync(string[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return;
        }

        // Member delete does NOT cascade to the login account, so delete the account(s) explicitly first.
        // Deleting the ApplicationUser removes its role assignments and triggers the customer module's
        // user-deleted handler that clears its OrganizationMemberships.
        using (var userManager = _userManagerFactory())
        {
            foreach (var memberId in ids)
            {
                var users = (await _userSearchService.SearchUsersAsync(
                    new UserSearchCriteria { MemberId = memberId, Take = int.MaxValue })).Results;

                foreach (var found in users)
                {
                    var user = await userManager.FindByIdAsync(found.Id);
                    if (user != null)
                    {
                        ThrowIfFailed(await userManager.DeleteAsync(user));
                    }
                }
            }
        }

        await _memberService.DeleteAsync(ids);
    }

    public virtual Task BlockAsync(string id)
    {
        return SetLockoutAsync(id, DateTimeOffset.MaxValue);
    }

    public virtual Task UnblockAsync(string id)
    {
        return SetLockoutAsync(id, null);
    }

    public virtual async Task SetPasswordAsync(string id, string newPassword)
    {
        using var userManager = _userManagerFactory();
        var user = await GetTrackedUserAsync(userManager, id)
            ?? throw new InvalidOperationException($"No account found for Sales Rep '{id}'.");
        await ResetPasswordAsync(userManager, user, newPassword);
    }

    public virtual async Task<IList<SalesRepRole>> GetRolesAsync()
    {
        var roles = await _roleResolver.GetSelectableRolesAsync();
        return roles
            .Select(r => new SalesRepRole { Id = r.Id, Name = r.Name })
            .ToList();
    }

    protected virtual async Task SetLockoutAsync(string id, DateTimeOffset? lockoutEnd)
    {
        using var userManager = _userManagerFactory();
        var user = await GetTrackedUserAsync(userManager, id)
            ?? throw new InvalidOperationException($"No account found for Sales Rep '{id}'.");
        await ApplyLockoutAsync(userManager, user, lockoutEnd);
    }

    /// <summary>Enable lockout and set the end date on a user already tracked by <paramref name="userManager"/>.</summary>
    protected static async Task ApplyLockoutAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, DateTimeOffset? lockoutEnd)
    {
        await userManager.SetLockoutEnabledAsync(user, true);
        ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user, lockoutEnd));
    }

    /// <summary>Reset the password of a user already tracked by <paramref name="userManager"/>.</summary>
    protected static async Task ResetPasswordAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string newPassword)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        ThrowIfFailed(await userManager.ResetPasswordAsync(user, token, newPassword));
    }

    protected virtual async Task<ApplicationUser> CreateAccountAsync(UserManager<ApplicationUser> userManager, Contact contact, SalesRepDetails salesRep, Role assignableRole)
    {
        var email = contact.Emails.FirstOrDefault();

        var user = AbstractTypeFactory<ApplicationUser>.TryCreateInstance();
        user.UserName = !string.IsNullOrEmpty(salesRep.UserName) ? salesRep.UserName : email;
        user.Email = email;
        user.MemberId = contact.Id;
        user.StoreId = salesRep.StoreId;
        user.UserType = "Customer";

        // Every new Sales Rep gets the global Sales Rep role assignment (deterministic seeded role).
        if (assignableRole != null)
        {
            user.Roles = [assignableRole];
        }

        var result = string.IsNullOrEmpty(salesRep.Password)
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, salesRep.Password);
        ThrowIfFailed(result);

        if (salesRep.IsLocked)
        {
            await ApplyLockoutAsync(userManager, user, DateTimeOffset.MaxValue);
        }

        return user;
    }

    protected virtual async Task<ApplicationUser> UpdateAccountAsync(UserManager<ApplicationUser> userManager, Contact contact, SalesRepDetails salesRep, Role assignableRole, ISet<string> grantingRoleIds)
    {
        var user = await GetTrackedUserAsync(userManager, contact.Id);
        if (user == null)
        {
            // The contact had no account yet (edge case) — create one.
            return await CreateAccountAsync(userManager, contact, salesRep, assignableRole);
        }

        // The login email is emails[0]. Keep both Email and UserName (the sign-in identifier) in sync with it
        // so they never diverge when the admin changes the login email.
        var loginEmail = contact.Emails.FirstOrDefault();
        if (!string.IsNullOrEmpty(loginEmail))
        {
            user.Email = loginEmail;
            user.UserName = loginEmail;
        }

        // Set the global role to the selected one: drop any other granting role, ensure the target is present.
        // (Switching the role re-points the global assignment.)
        var roles = (user.Roles ?? []).Where(r => !grantingRoleIds.Contains(r.Id)).ToList();
        if (assignableRole != null)
        {
            roles.Add(assignableRole);
        }
        user.Roles = roles;

        ThrowIfFailed(await userManager.UpdateAsync(user));

        // Apply lockout + password on the user already tracked by this UserManager (no extra fetch/manager).
        await ApplyLockoutAsync(userManager, user, salesRep.IsLocked ? DateTimeOffset.MaxValue : null);

        if (!string.IsNullOrEmpty(salesRep.Password))
        {
            await ResetPasswordAsync(userManager, user, salesRep.Password);
        }

        return user;
    }

    protected virtual async Task SyncMembershipsAsync(string userId, SalesRepDetails salesRep, Role assignableRole, ISet<string> grantingRoleIds)
    {
        var servedOrgIds = DistinctNonEmpty(salesRep.Organizations?.Select(o => o.OrganizationId));
        var existing = await GetAllMembershipsAsync(userId);

        var toSave = new List<OrganizationMembership>();
        var toDelete = new List<string>();

        GrantOnServedOrgs(servedOrgIds, existing, userId, assignableRole, grantingRoleIds, toSave);
        RevokeFromUnservedOrgs(servedOrgIds, existing, grantingRoleIds, toSave, toDelete);

        if (toSave.Count > 0)
        {
            await _membershipService.SaveChangesAsync(toSave);
        }
        if (toDelete.Count > 0)
        {
            await _membershipService.DeleteAsync(toDelete);
        }
    }

    /// <summary>Grant the selected role on every served org, creating the membership when absent and
    /// re-pointing an existing one (dropping any other granting role) so a role change takes effect.</summary>
    protected virtual void GrantOnServedOrgs(IList<string> servedOrgIds, IList<OrganizationMembership> existing, string userId, Role assignableRole, ISet<string> grantingRoleIds, List<OrganizationMembership> toSave)
    {
        var existingByOrg = existing.ToDictionary(m => m.OrganizationId, m => m);
        foreach (var orgId in servedOrgIds)
        {
            if (!existingByOrg.TryGetValue(orgId, out var membership))
            {
                toSave.Add(CreateMembership(userId, orgId, assignableRole));
            }
            else if (TryRepointMembershipRole(membership, assignableRole, grantingRoleIds))
            {
                toSave.Add(membership);
            }
        }
    }

    /// <summary>Revoke the granting role from memberships of orgs no longer served, deleting a membership
    /// left with no roles.</summary>
    protected static void RevokeFromUnservedOrgs(IList<string> servedOrgIds, IList<OrganizationMembership> existing, ISet<string> grantingRoleIds, List<OrganizationMembership> toSave, List<string> toDelete)
    {
        var unserved = existing.Where(m => !servedOrgIds.Contains(m.OrganizationId)
            && m.Roles.Any(r => grantingRoleIds.Contains(r.RoleId)));
        foreach (var membership in unserved)
        {
            membership.Roles = [.. membership.Roles.Where(r => !grantingRoleIds.Contains(r.RoleId))];
            if (membership.Roles.Count == 0)
            {
                toDelete.Add(membership.Id);
            }
            else
            {
                toSave.Add(membership);
            }
        }
    }

    /// <summary>Re-point an existing membership to the selected role; returns true when it changed.</summary>
    protected virtual bool TryRepointMembershipRole(OrganizationMembership membership, Role assignableRole, ISet<string> grantingRoleIds)
    {
        var alreadyCorrect = membership.Roles.Any(r => r.RoleId == assignableRole.Id);
        var hasOtherGranting = membership.Roles.Any(r => grantingRoleIds.Contains(r.RoleId) && r.RoleId != assignableRole.Id);
        if (alreadyCorrect && !hasOtherGranting)
        {
            return false;
        }

        membership.Roles = [
            .. membership.Roles.Where(r => !grantingRoleIds.Contains(r.RoleId)),
            CreateMembershipRole(assignableRole),
        ];
        return true;
    }

    protected virtual OrganizationMembership CreateMembership(string userId, string orgId, Role assignableRole)
    {
        var created = AbstractTypeFactory<OrganizationMembership>.TryCreateInstance();
        created.UserId = userId;
        created.OrganizationId = orgId;
        created.Roles = [CreateMembershipRole(assignableRole)];
        return created;
    }

    protected virtual OrganizationMembershipRole CreateMembershipRole(Role role)
    {
        var membershipRole = AbstractTypeFactory<OrganizationMembershipRole>.TryCreateInstance();
        membershipRole.RoleId = role.Id;
        membershipRole.RoleName = role.Name;
        return membershipRole;
    }

    protected virtual async Task<ApplicationUser> FindUserByMemberIdAsync(string memberId)
    {
        var result = await _userSearchService.SearchUsersAsync(new UserSearchCriteria { MemberId = memberId, Take = 1 });
        return result.Results.FirstOrDefault();
    }

    protected virtual async Task<ApplicationUser> GetTrackedUserAsync(UserManager<ApplicationUser> userManager, string memberId)
    {
        var found = await FindUserByMemberIdAsync(memberId);
        return found == null ? null : await userManager.FindByIdAsync(found.Id);
    }

    /// <summary>All memberships of a user that carry a role granting the sales-rep permission.</summary>
    protected virtual async Task<IList<OrganizationMembership>> GetSalesRepMembershipsAsync(string userId, ISet<string> grantingRoleIds)
    {
        var all = await GetAllMembershipsAsync(userId);
        return all.Where(m => m.Roles.Any(r => grantingRoleIds.Contains(r.RoleId))).ToList();
    }

    protected virtual async Task<IList<OrganizationMembership>> GetAllMembershipsAsync(string userId)
    {
        var result = await _membershipSearchService.SearchAsync(new OrganizationMembershipSearchCriteria
        {
            UserId = userId,
            Take = int.MaxValue,
        });
        return result.Results;
    }

    protected virtual void ApplyProfile(Contact contact, SalesRepDetails salesRep)
    {
        contact.Salutation = salesRep.Salutation;
        contact.FirstName = salesRep.FirstName;
        contact.MiddleName = salesRep.MiddleName;
        contact.LastName = salesRep.LastName;

        var fullName = DeriveFullName(salesRep);
        contact.FullName = fullName;
        // Persist the Name column so SQL search/sort by name works.
        contact.Name = fullName;
        contact.BirthDate = salesRep.BirthDate;
        contact.TimeZone = salesRep.TimeZone;
        contact.DefaultLanguage = salesRep.DefaultLanguage;
        contact.CurrencyCode = salesRep.CurrencyCode;
        contact.About = salesRep.About;
        contact.PhotoUrl = salesRep.PhotoUrl;
        contact.Status = !string.IsNullOrEmpty(salesRep.Status) ? salesRep.Status : contact.Status;

        // Login (emails[0]) + additional emails as one de-duplicated list (case-insensitive, order preserved
        // so the login stays first). The login email cannot be dropped here (it's the account).
        contact.Emails = DistinctNonEmpty(salesRep.Emails);
        contact.Phones = DistinctNonEmpty(salesRep.Phones);
        contact.Addresses = salesRep.Addresses?.ToList() ?? [];
        contact.Organizations = DistinctNonEmpty(salesRep.Organizations?.Select(o => o.OrganizationId));
    }

    /// <summary>(Re)derive the full name from the name parts so editing First/Middle/Last refreshes Name/FullName
    /// (the blade has no FullName field). Fall back to a passed FullName or the login email when no parts exist.</summary>
    protected static string DeriveFullName(SalesRepDetails salesRep)
    {
        var fullName = string.Join(' ', new[] { salesRep.FirstName, salesRep.MiddleName, salesRep.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return !string.IsNullOrWhiteSpace(salesRep.FullName) ? salesRep.FullName : salesRep.Emails?.FirstOrDefault();
    }

    /// <summary>Trim out null/blank values and de-duplicate case-insensitively, preserving order.</summary>
    protected static List<string> DistinctNonEmpty(IEnumerable<string> values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    protected virtual SalesRepDetails ToSalesRep(Contact contact, ApplicationUser user)
    {
        return new SalesRepDetails
        {
            Id = contact.Id,
            UserId = user?.Id,
            UserName = user?.UserName,
            Salutation = contact.Salutation,
            FirstName = contact.FirstName,
            MiddleName = contact.MiddleName,
            LastName = contact.LastName,
            FullName = contact.FullName,
            BirthDate = contact.BirthDate,
            TimeZone = contact.TimeZone,
            DefaultLanguage = contact.DefaultLanguage,
            CurrencyCode = contact.CurrencyCode,
            About = contact.About,
            PhotoUrl = contact.PhotoUrl,
            Status = contact.Status,
            Emails = contact.Emails?.ToList() ?? [],
            Phones = contact.Phones?.ToList() ?? [],
            Addresses = contact.Addresses?.ToList() ?? [],
            StoreId = user?.StoreId,
            IsLocked = IsLocked(user),
        };
    }

    protected static bool IsLocked(ApplicationUser user)
    {
        return user?.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
    }

    protected static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
