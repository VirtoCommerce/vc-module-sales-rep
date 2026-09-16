using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepStoreAccessService : ISalesRepStoreAccessService
{
    private readonly IStoreService _storeService;
    private readonly Func<UserManager<ApplicationUser>> _userManagerFactory;

    public SalesRepStoreAccessService(IStoreService storeService, Func<UserManager<ApplicationUser>> userManagerFactory)
    {
        _storeService = storeService;
        _userManagerFactory = userManagerFactory;
    }

    public virtual async Task<bool> IsAllowedAsync(string userId, string storeId)
    {
        // No store named, nothing claimed: the caller's own scoping (the organizations they serve) still applies.
        if (string.IsNullOrEmpty(storeId))
        {
            return true;
        }

        var caller = await GetUserAsync(userId);
        var callerStoreId = caller?.StoreId;

        // An administrator is not bound to a store. "No store" is not proof of being one, though —
        // SalesRepDetails.StoreId is optional and unvalidated — so a rep saved without one must not pass here.
        if (string.IsNullOrEmpty(callerStoreId))
        {
            return caller?.IsAdministrator == true;
        }

        if (storeId.EqualsIgnoreCase(callerStoreId))
        {
            return true;
        }

        // A store may trust others (the platform's cross-store sharing); the named store's list decides.
        var store = await _storeService.GetNoCloneAsync(storeId);

        return store?.TrustedGroups?.Any(x => x.EqualsIgnoreCase(callerStoreId)) == true;
    }

    // A lookup by primary key, on every rep-facing query that names a store: CustomUserManager.FindByIdAsync is
    // memory-cached with a change token, where routing the same question through the user SEARCH service costs a
    // count, a paged query including the roles, and a roles query per hit — uncached, every time.
    protected virtual async Task<ApplicationUser> GetUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        using var userManager = _userManagerFactory();

        return await userManager.FindByIdAsync(userId);
    }
}
