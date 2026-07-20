using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Ranks the products a Sales Rep sold (VCST-5309, dashboard/customer "Top Sellers"). Aggregates the rep's own order
/// line items grouped by product — units = Σ quantity, revenue = Σ (quantity × unit price) — over the criteria's
/// optional date range and category subtree, returning the top-N by the requested metric, converted to one currency.
/// Reads the Orders EF store directly (the same scoped .Data exception the statistics services use); the row's display
/// data (name / sku / image / category) comes from the line-item snapshot, so no catalog read is needed here.
/// </summary>
public interface ISalesRepTopSellerService
{
    Task<IList<SalesRepTopSeller>> GetTopSellersAsync(SalesRepTopSellerCriteria criteria);

    /// <summary>
    /// The rep's distinct sold product ids within the criteria's scope (organizations / creator / store / date range,
    /// excluding cancelled and prototype orders) — the same scoped line-item set the ranking runs over. The category
    /// filter uses this to bound its catalog-index lookup to the products the rep actually sold (so the index is never
    /// asked to enumerate a whole category), which also keeps the data-isolation invariant (creator scope) intact.
    /// </summary>
    Task<IList<string>> GetSoldProductIdsAsync(SalesRepTopSellerCriteria criteria);
}
