using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepStoreAccessService : ISalesRepStoreAccessService
{
    private readonly IStoreService _storeService;
    private readonly IUserSearchService _userSearchService;

    public SalesRepStoreAccessService(IStoreService storeService, IUserSearchService userSearchService)
    {
        _storeService = storeService;
        _userSearchService = userSearchService;
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

        // An administrator or a service account is not bound to a store, and their access is decided by the
        // organizations they serve. "No store" is not proof of being one, though: SalesRepDetails.StoreId is an
        // optional string with no validation behind it, so a rep saved without one would otherwise be able to
        // name any store — and storeId chooses which analytics property is read and whose orders are counted.
        if (string.IsNullOrEmpty(callerStoreId))
        {
            return caller?.IsAdministrator == true;
        }

        if (storeId.EqualsIgnoreCase(callerStoreId))
        {
            return true;
        }

        // A store may trust others (the platform's cross-store sharing); the named store is the one whose
        // trust list decides, exactly as the communication command reads it.
        var store = await _storeService.GetNoCloneAsync(storeId);

        return store?.TrustedGroups?.Any(x => x.EqualsIgnoreCase(callerStoreId)) == true;
    }

    protected virtual async Task<ApplicationUser> GetUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var criteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = [userId];
        criteria.Take = 1;

        return (await _userSearchService.SearchUsersAsync(criteria)).Results.FirstOrDefault();
    }
}
