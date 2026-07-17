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
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;
using CustomerSettings = VirtoCommerce.CustomerModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepService : ISalesRepService
{
    private readonly IMemberService _memberService;
    private readonly IUserSearchService _userSearchService;
    private readonly IOrganizationMembershipService _membershipService;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IStoreService _storeService;
    private readonly Func<UserManager<ApplicationUser>> _userManagerFactory;

    public SalesRepService(
        IMemberService memberService,
        IUserSearchService userSearchService,
        IOrganizationMembershipService membershipService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver,
        IStoreService storeService,
        Func<UserManager<ApplicationUser>> userManagerFactory)
    {
        _memberService = memberService;
        _userSearchService = userSearchService;
        _membershipService = membershipService;
        _membershipSearchService = membershipSearchService;
        _roleResolver = roleResolver;
        _storeService = storeService;
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
                .Select(m =>
                {
                    var org = AbstractTypeFactory<SalesRepOrganization>.TryCreateInstance();
                    org.OrganizationId = m.OrganizationId;
                    org.OrganizationName = m.OrganizationName;
                    org.MembershipId = m.Id;
                    return org;
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
        ValidateAddresses(salesRep);

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

        // Only read the store default when the incoming model has no status. New reps carry none (the blade has
        // no status field), so they get seeded here; an edit round-trips the rep's existing status, so the store
        // read is skipped when it wouldn't be used (ApplyProfile prefers the incoming status over the default).
        var defaultContactStatus = string.IsNullOrEmpty(salesRep.Status)
            ? await ResolveDefaultContactStatusAsync(salesRep.StoreId)
            : null;
        ApplyProfile(contact, salesRep, defaultContactStatus);
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
            //
            // NOTE: this compensation is intentionally CREATE-ONLY. On update the contact profile is saved
            // first, so if the later account/role/membership sync throws, the rep is left partially updated
            // with no rollback. That is a conscious tradeoff (no cross-service transaction is available, and
            // rolling an update back to its prior state would require snapshotting every touched aggregate);
            // an update failure surfaces as an error and the admin can re-save.
            await TryRollbackContactAsync(contact.Id);
            throw;
        }

        return await GetByIdAsync(salesRep.Id);
    }

    /// <summary>
    /// Reject addresses missing the fields required to persist them. Country is mandatory because the customer
    /// module resolves the country name/regions from <c>CountryCode</c> on save (an empty or unknown code throws
    /// deep in the platform countries service — a NullReferenceException surfacing as an opaque 500). City, Line 1
    /// and Postal code are the required fields of the classic contact-address form; City is additionally enforced
    /// by a NOT NULL constraint on the Address table. This is the API-side counterpart to the blade's required-field
    /// validation, so a malformed payload from any client fails fast with a clear message instead of a 500.
    /// </summary>
    protected virtual void ValidateAddresses(SalesRepDetails salesRep)
    {
        if (salesRep.Addresses.IsNullOrEmpty())
        {
            return;
        }

        for (var i = 0; i < salesRep.Addresses.Count; i++)
        {
            var address = salesRep.Addresses[i];
            List<string> missing = [];
            if (string.IsNullOrWhiteSpace(address.CountryCode))
            {
                missing.Add("country");
            }
            if (string.IsNullOrWhiteSpace(address.City))
            {
                missing.Add("city");
            }
            if (string.IsNullOrWhiteSpace(address.Line1))
            {
                missing.Add("address line 1");
            }
            if (string.IsNullOrWhiteSpace(address.PostalCode))
            {
                missing.Add("postal code");
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"Address {i + 1} is missing required field(s): {string.Join(", ", missing)}.");
            }
        }
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
        //
        // One batched, internally-paged search for the accounts of ALL member ids (UserSearchCriteria.MemberIds)
        // — not a query per id, and not an unbounded single page (SearchAllAsync pages internally).
        var searchCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        searchCriteria.MemberIds = ids;
        var accounts = await _userSearchService.SearchAllAsync(searchCriteria);

        if (accounts.Count > 0)
        {
            using var userManager = _userManagerFactory();
            foreach (var found in accounts)
            {
                // FindByIdAsync gets the managed entity required for deletion (search results are detached).
                var user = await userManager.FindByIdAsync(found.Id);
                if (user != null)
                {
                    ThrowIfFailed(await userManager.DeleteAsync(user));
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
            .Select(r =>
            {
                var role = AbstractTypeFactory<SalesRepRole>.TryCreateInstance();
                role.Id = r.Id;
                role.Name = r.Name;
                return role;
            })
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
        var account = await GetTrackedUserAsync(userManager, contact.Id);
        if (account == null)
        {
            // The contact had no account yet (edge case) — create one.
            return await CreateAccountAsync(userManager, contact, salesRep, assignableRole);
        }

        // UpdateAsync must receive a DETACHED user carrying the desired state — the same contract the platform's
        // own PUT /api/platform/security/users relies on (its payload is JSON-bound, never the manager's instance).
        // The instance FindByIdAsync returns is the shared memory-cached one and, right after a cache miss, is ALSO
        // tracked by this manager's DbContext. Passing it to UpdateAsync corrupts the role update: the platform's
        // UpdateUserAsync re-loads "the existing user" through that same context, EF identity resolution hands back
        // the very same instance, and LoadUserDetailsAsync resets its Roles from the DB — so the platform then diffs
        // the desired roles against themselves and silently drops the change. Editing a clone also keeps mutations
        // from leaking into the shared cache when a save fails midway.
        var user = account.CloneTyped();

        // The login email is emails[0]. Keep both Email and UserName (the sign-in identifier) in sync with it
        // so they never diverge when the admin changes the login email.
        var loginEmail = contact.Emails.FirstOrDefault();
        if (!string.IsNullOrEmpty(loginEmail))
        {
            user.Email = loginEmail;
            user.UserName = loginEmail;
        }

        // Set the global role to the selected one: drop any other granting role, keep unrelated roles, ensure the
        // target is present. UpdateAsync diffs this desired set against the persisted assignments and applies the
        // difference. (Switching the role re-points the global assignment.)
        var roles = (user.Roles ?? []).Where(r => !grantingRoleIds.Contains(r.Id)).ToList();
        if (assignableRole != null)
        {
            roles.Add(assignableRole);
        }
        user.Roles = roles;

        ThrowIfFailed(await userManager.UpdateAsync(user));

        // Lockout + password reuse the same detached user; the manager persists them by patching the stored entity.
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

        List<OrganizationMembership> toSave = [];
        List<string> toDelete = [];

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
        // One membership per org is expected, but guard against duplicates (bad data) rather than letting
        // ToDictionary throw — keep the first membership for each org.
        var existingByOrg = existing
            .GroupBy(m => m.OrganizationId)
            .ToDictionary(g => g.Key, g => g.First());
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
        var criteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        criteria.MemberId = memberId;
        criteria.Take = 1;
        var result = await _userSearchService.SearchUsersAsync(criteria);
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

    protected virtual Task<IList<OrganizationMembership>> GetAllMembershipsAsync(string userId)
    {
        // SearchAllAsync pages internally (IOrganizationMembershipSearchService : ISearchService) — no unbounded Take.
        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserId = userId;
        return _membershipSearchService.SearchAllAsync(criteria);
    }

    /// <summary>Resolves the store's configured default contact status (<c>Customer.ContactDefaultStatus</c>) so a
    /// Sales Rep is seeded with the same member status the store would give a self-registered contact (e.g. "Approved"
    /// = Active in the storefront member list) instead of an empty status that renders as "Inactive". Returns null
    /// when there is no store bound to the rep or the setting is unset — mirrors <c>ExternalSignInUserBuilder</c>.</summary>
    protected virtual async Task<string> ResolveDefaultContactStatusAsync(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await _storeService.GetNoCloneAsync(storeId);
        return store?.Settings.GetValue<string>(CustomerSettings.ContactDefaultStatus);
    }

    protected virtual void ApplyProfile(Contact contact, SalesRepDetails salesRep, string defaultStatus)
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
        // Status precedence: an explicit status on the incoming model wins; otherwise fall back to the store's
        // configured default contact status so the rep shows the right member status in the storefront (e.g.
        // "Active") rather than "Inactive". When neither is available the current status is left untouched.
        // Blocked reps are represented by account lockout (not this status), so overwriting it here is safe.
        contact.Status = salesRep.Status.EmptyToNull() ?? defaultStatus.EmptyToNull() ?? contact.Status;

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
        string[] nameParts = [salesRep.FirstName, salesRep.MiddleName, salesRep.LastName];
        var fullName = string.Join(' ', nameParts.Where(x => !string.IsNullOrWhiteSpace(x)));
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
        var result = AbstractTypeFactory<SalesRepDetails>.TryCreateInstance();
        result.Id = contact.Id;
        result.UserId = user?.Id;
        result.UserName = user?.UserName;
        result.Salutation = contact.Salutation;
        result.FirstName = contact.FirstName;
        result.MiddleName = contact.MiddleName;
        result.LastName = contact.LastName;
        result.FullName = contact.FullName;
        result.BirthDate = contact.BirthDate;
        result.TimeZone = contact.TimeZone;
        result.DefaultLanguage = contact.DefaultLanguage;
        result.CurrencyCode = contact.CurrencyCode;
        result.About = contact.About;
        result.PhotoUrl = contact.PhotoUrl;
        result.Status = contact.Status;
        result.Emails = contact.Emails?.ToList() ?? [];
        result.Phones = contact.Phones?.ToList() ?? [];
        result.Addresses = contact.Addresses?.ToList() ?? [];
        result.StoreId = user?.StoreId;
        result.IsLocked = IsLocked(user);
        return result;
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
