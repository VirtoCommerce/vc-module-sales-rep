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
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly Func<UserManager<ApplicationUser>> _userManagerFactory;

    public SalesRepService(
        IMemberService memberService,
        IUserSearchService userSearchService,
        IOrganizationMembershipService membershipService,
        ISalesRepRoleResolver roleResolver,
        Func<UserManager<ApplicationUser>> userManagerFactory)
    {
        _memberService = memberService;
        _userSearchService = userSearchService;
        _membershipService = membershipService;
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

    public virtual async Task<SalesRepDetails> SaveChangesAsync(SalesRepDetails salesRep)
    {
        ArgumentNullException.ThrowIfNull(salesRep);

        var isNew = string.IsNullOrEmpty(salesRep.Id);

        var contact = isNew
            ? AbstractTypeFactory<Contact>.TryCreateInstance()
            : await _memberService.GetByIdAsync(salesRep.Id, MemberResponseGroup.Full.ToString()) as Contact
              ?? throw new InvalidOperationException($"Sales Rep '{salesRep.Id}' not found");

        ApplyProfile(contact, salesRep);
        await _memberService.SaveChangesAsync([contact]);
        salesRep.Id = contact.Id;

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

        return await GetByIdAsync(salesRep.Id);
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
        var user = await GetTrackedUserAsync(userManager, id);
        if (user == null)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        ThrowIfFailed(result);
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
        var user = await GetTrackedUserAsync(userManager, id);
        if (user == null)
        {
            return;
        }

        await userManager.SetLockoutEnabledAsync(user, true);
        var result = await userManager.SetLockoutEndDateAsync(user, lockoutEnd);
        ThrowIfFailed(result);
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
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
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

        var result = await userManager.UpdateAsync(user);
        ThrowIfFailed(result);

        await SetLockoutAsync(contact.Id, salesRep.IsLocked ? DateTimeOffset.MaxValue : null);

        if (!string.IsNullOrEmpty(salesRep.Password))
        {
            await SetPasswordAsync(contact.Id, salesRep.Password);
        }

        return user;
    }

    protected virtual async Task SyncMembershipsAsync(string userId, SalesRepDetails salesRep, Role assignableRole, ISet<string> grantingRoleIds)
    {
        var servedOrgIds = salesRep.Organizations?
            .Select(o => o.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList() ?? [];

        var existing = await GetAllMembershipsAsync(userId);
        var existingByOrg = existing.ToDictionary(m => m.OrganizationId, m => m);

        var toSave = new List<OrganizationMembership>();
        var toDelete = new List<string>();

        // Grant the selected role on served orgs (creating the membership when absent). On an existing
        // membership, drop any other granting role and set the selected one so a role change re-points it.
        foreach (var orgId in servedOrgIds)
        {
            if (existingByOrg.TryGetValue(orgId, out var membership))
            {
                var alreadyCorrect = membership.Roles.Any(r => r.RoleId == assignableRole.Id);
                var hasOtherGranting = membership.Roles.Any(r => grantingRoleIds.Contains(r.RoleId) && r.RoleId != assignableRole.Id);
                if (!alreadyCorrect || hasOtherGranting)
                {
                    membership.Roles = [
                        .. membership.Roles.Where(r => !grantingRoleIds.Contains(r.RoleId)),
                        CreateMembershipRole(assignableRole),
                    ];
                    toSave.Add(membership);
                }
            }
            else
            {
                var created = AbstractTypeFactory<OrganizationMembership>.TryCreateInstance();
                created.UserId = userId;
                created.OrganizationId = orgId;
                created.Roles = [CreateMembershipRole(assignableRole)];
                toSave.Add(created);
            }
        }

        // Revoke the sales-rep role from orgs no longer served.
        foreach (var membership in existing.Where(m => !servedOrgIds.Contains(m.OrganizationId)))
        {
            if (membership.Roles.Any(r => grantingRoleIds.Contains(r.RoleId)))
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

        if (toSave.Count > 0)
        {
            await _membershipService.SaveChangesAsync(toSave);
        }
        if (toDelete.Count > 0)
        {
            await _membershipService.DeleteAsync(toDelete);
        }
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
        var result = await _membershipService.SearchAsync(new OrganizationMembershipSearchCriteria
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
        // Always (re)derive the full name from the name parts, so editing First/Middle/Last refreshes
        // Name/FullName (the blade has no FullName field — it is derived). Fall back to a passed FullName
        // or the login email when no name parts are present.
        var fullName = string.Join(' ', new[] { salesRep.FirstName, salesRep.MiddleName, salesRep.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = !string.IsNullOrWhiteSpace(salesRep.FullName) ? salesRep.FullName : salesRep.Emails?.FirstOrDefault();
        }
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

        // Combine login (emails[0]) + additional emails into one de-duplicated list (case-insensitive,
        // order preserved so the login stays first). The login email cannot be dropped (it's the account).
        contact.Emails = salesRep.Emails?
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        contact.Phones = salesRep.Phones?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        contact.Addresses = salesRep.Addresses?.ToList() ?? [];

        contact.Organizations = salesRep.Organizations?
            .Select(o => o.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
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
