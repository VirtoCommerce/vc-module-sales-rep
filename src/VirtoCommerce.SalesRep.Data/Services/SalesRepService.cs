using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    private readonly AbstractValidator<SalesRepDetails> _validator;
    private readonly ILogger<SalesRepService> _logger;

    public SalesRepService(
        IMemberService memberService,
        IUserSearchService userSearchService,
        IOrganizationMembershipService membershipService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver,
        IStoreService storeService,
        Func<UserManager<ApplicationUser>> userManagerFactory,
        AbstractValidator<SalesRepDetails> validator,
        ILogger<SalesRepService> logger)
    {
        _memberService = memberService;
        _userSearchService = userSearchService;
        _membershipService = membershipService;
        _membershipSearchService = membershipSearchService;
        _roleResolver = roleResolver;
        _storeService = storeService;
        _userManagerFactory = userManagerFactory;
        _validator = validator;
        _logger = logger;
    }

    public virtual async Task<IList<SalesRepDetails>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        var result = new List<SalesRepDetails>();
        if (ids != null)
        {
            foreach (var id in ids)
            {
                var salesRep = await LoadSalesRepAsync(id);
                if (salesRep != null)
                {
                    result.Add(salesRep);
                }
            }
        }
        return result;
    }

    protected virtual async Task<SalesRepDetails> LoadSalesRepAsync(string id)
    {
        if (await _memberService.GetByIdAsync(id, nameof(MemberResponseGroup.Full)) is not Contact contact)
        {
            return null;
        }

        var user = await FindUserByMemberIdAsync(contact.Id);
        var result = ToSalesRep(contact, user);

        if (user != null)
        {
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

    public virtual async Task SaveChangesAsync(IList<SalesRepDetails> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        foreach (var model in models)
        {
            await SaveOneAsync(model);
        }
    }

    protected virtual async Task SaveOneAsync(SalesRepDetails salesRep)
    {
        ArgumentNullException.ThrowIfNull(salesRep);

        Normalize(salesRep);
        await ValidateAsync(salesRep);

        var isNew = string.IsNullOrEmpty(salesRep.Id);

        var contact = await SaveContactAsync(salesRep, isNew);
        salesRep.Id = contact.Id;

        try
        {
            var (assignableRole, grantingRoleIds) = await ResolveAssignableRoleAsync(salesRep);
            var user = await SaveAccountAsync(contact, salesRep, isNew, assignableRole, grantingRoleIds);
            if (user != null)
            {
                await SyncMembershipsAsync(user.Id, salesRep, assignableRole, grantingRoleIds);
            }
        }
        catch when (isNew)
        {
            await TryRollbackContactAsync(contact.Id);
            throw;
        }
    }

    protected virtual async Task<Contact> SaveContactAsync(SalesRepDetails salesRep, bool isNew)
    {
        var contact = isNew
            ? AbstractTypeFactory<Contact>.TryCreateInstance()
            : await _memberService.GetByIdAsync(salesRep.Id, nameof(MemberResponseGroup.Full)) as Contact
              ?? throw new InvalidOperationException($"Sales Rep '{salesRep.Id}' not found");

        var defaultContactStatus = string.IsNullOrEmpty(salesRep.Status)
            ? await ResolveDefaultContactStatusAsync(salesRep.StoreId)
            : null;
        ApplyProfile(contact, salesRep, defaultContactStatus);
        await _memberService.SaveChangesAsync([contact]);
        return contact;
    }

    protected virtual async Task<ApplicationUser> SaveAccountAsync(Contact contact, SalesRepDetails salesRep, bool isNew, Role assignableRole, ISet<string> grantingRoleIds)
    {
        using var userManager = _userManagerFactory();
        return isNew
            ? await CreateAccountAsync(userManager, contact, salesRep, assignableRole)
            : await UpdateAccountAsync(userManager, contact, salesRep, assignableRole, grantingRoleIds);
    }

    protected virtual async Task<(Role AssignableRole, ISet<string> GrantingRoleIds)> ResolveAssignableRoleAsync(SalesRepDetails salesRep)
    {
        var grantingRoles = await _roleResolver.GetRolesGrantingAccessAsync();
        var assignableRole = grantingRoles.FirstOrDefault(r => r.Id == salesRep.RoleId)
            ?? await _roleResolver.EnsureSalesRepRoleAsync();
        var grantingRoleIds = grantingRoles.Select(r => r.Id).Append(assignableRole.Id).ToHashSet();
        return (assignableRole, grantingRoleIds);
    }

    // VC-Shell's VcInput emits the raw value, so trim every field the blade lets an admin type before
    // validating: a whitespace-only name must fail NotEmpty instead of being persisted and flowing into
    // FullName/Name, and padded emails must not become the account's user name.
    protected virtual void Normalize(SalesRepDetails salesRep)
    {
        salesRep.Salutation = salesRep.Salutation?.Trim();
        salesRep.FirstName = salesRep.FirstName?.Trim();
        salesRep.MiddleName = salesRep.MiddleName?.Trim();
        salesRep.LastName = salesRep.LastName?.Trim();
        salesRep.About = salesRep.About?.Trim();
        salesRep.UserName = salesRep.UserName?.Trim();

        foreach (var address in salesRep.Addresses ?? [])
        {
            NormalizeAddress(address);
        }
    }

    protected virtual void NormalizeAddress(Address address)
    {
        address.FirstName = address.FirstName?.Trim();
        address.LastName = address.LastName?.Trim();
        address.Line1 = address.Line1?.Trim();
        address.Line2 = address.Line2?.Trim();
        address.City = address.City?.Trim();
        address.RegionName = address.RegionName?.Trim();
        address.PostalCode = address.PostalCode?.Trim();
        address.CountryCode = address.CountryCode?.Trim();
        address.CountryName = address.CountryName?.Trim();
        address.Phone = address.Phone?.Trim();
        address.Email = address.Email?.Trim();
    }

    protected virtual Task ValidateAsync(SalesRepDetails salesRep)
    {
        return _validator.ValidateAndThrowAsync(salesRep);
    }

    protected virtual async Task TryRollbackContactAsync(string memberId)
    {
        try
        {
            await DeleteAsync([memberId]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to roll back contact '{MemberId}' after a failed Sales Rep create; it may be left orphaned.", memberId);
        }
    }

    public virtual async Task DeleteAsync(IList<string> ids, bool softDelete = false)
    {
        if (ids == null || ids.Count == 0)
        {
            return;
        }

        var searchCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        searchCriteria.MemberIds = ids;
        var accounts = await _userSearchService.SearchAllAsync(searchCriteria);

        if (accounts.Count > 0)
        {
            using var userManager = _userManagerFactory();
            foreach (var found in accounts)
            {
                var user = await userManager.FindByIdAsync(found.Id);
                if (user != null)
                {
                    ThrowIfFailed(await userManager.DeleteAsync(user));
                }
            }
        }

        await _memberService.DeleteAsync(ids.ToArray());
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

    protected static async Task ApplyLockoutAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, DateTimeOffset? lockoutEnd)
    {
        await userManager.SetLockoutEnabledAsync(user, true);
        ThrowIfFailed(await userManager.SetLockoutEndDateAsync(user, lockoutEnd));
    }

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
            return await CreateAccountAsync(userManager, contact, salesRep, assignableRole);
        }

        // Edit a DETACHED clone, never the FindByIdAsync instance: on a cache miss that instance is also tracked by
        // this manager's DbContext, and UpdateAsync would then diff the roles against themselves and silently drop the change.
        var user = account.CloneTyped();

        var loginEmail = contact.Emails.FirstOrDefault();
        if (!string.IsNullOrEmpty(loginEmail))
        {
            user.Email = loginEmail;
            user.UserName = loginEmail;
        }

        var roles = (user.Roles ?? []).Where(r => !grantingRoleIds.Contains(r.Id)).ToList();
        if (assignableRole != null)
        {
            roles.Add(assignableRole);
        }
        user.Roles = roles;

        ThrowIfFailed(await userManager.UpdateAsync(user));

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

    protected virtual void GrantOnServedOrgs(IList<string> servedOrgIds, IList<OrganizationMembership> existing, string userId, Role assignableRole, ISet<string> grantingRoleIds, List<OrganizationMembership> toSave)
    {
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

    protected virtual async Task<IList<OrganizationMembership>> GetSalesRepMembershipsAsync(string userId, ISet<string> grantingRoleIds)
    {
        var all = await GetAllMembershipsAsync(userId);
        return all.Where(m => m.Roles.Any(r => grantingRoleIds.Contains(r.RoleId))).ToList();
    }

    protected virtual Task<IList<OrganizationMembership>> GetAllMembershipsAsync(string userId)
    {
        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserId = userId;
        return _membershipSearchService.SearchAllAsync(criteria);
    }

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
        contact.Name = fullName;
        contact.BirthDate = salesRep.BirthDate;
        contact.TimeZone = salesRep.TimeZone;
        contact.DefaultLanguage = salesRep.DefaultLanguage;
        contact.CurrencyCode = salesRep.CurrencyCode;
        contact.About = salesRep.About;
        contact.PhotoUrl = salesRep.PhotoUrl;
        contact.Status = salesRep.Status.EmptyToNull() ?? defaultStatus.EmptyToNull() ?? contact.Status;

        contact.Emails = DistinctNonEmpty(salesRep.Emails);
        contact.Phones = DistinctNonEmpty(salesRep.Phones);
        contact.Addresses = salesRep.Addresses?.ToList() ?? [];
        contact.Organizations = DistinctNonEmpty(salesRep.Organizations?.Select(o => o.OrganizationId));
    }

    // First and last name are required (SalesRepDetailsValidator), so the parts always yield a name; only the
    // optional middle name has to be filtered out.
    protected static string DeriveFullName(SalesRepDetails salesRep)
    {
        string[] nameParts = [salesRep.FirstName, salesRep.MiddleName, salesRep.LastName];
        return string.Join(' ', nameParts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    protected static List<string> DistinctNonEmpty(IEnumerable<string> values)
    {
        return values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
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
