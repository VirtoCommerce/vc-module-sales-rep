using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

// A rep account is bound to one store, so a store id from the client is a claim to check, not a filter to trust.
public interface ISalesRepStoreAccessService
{
    // True for the caller's own store or one it trusts; an empty storeId names no store and passes.
    Task<bool> IsAllowedAsync(string userId, string storeId);
}
