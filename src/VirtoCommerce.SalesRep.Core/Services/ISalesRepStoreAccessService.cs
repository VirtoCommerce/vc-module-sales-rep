using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Whether a caller may act on, or read from, a store they named. A sales rep account is bound to one store,
/// so a store id arriving from the client is a claim to check rather than a filter to trust.
/// </summary>
public interface ISalesRepStoreAccessService
{
    /// <summary>
    /// True when the caller's own store is <paramref name="storeId"/>, or trusts it. An empty
    /// <paramref name="storeId"/> is allowed: naming no store is not a claim about one.
    /// </summary>
    Task<bool> IsAllowedAsync(string userId, string storeId);
}
