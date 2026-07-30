using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepTopSellerService
{
    Task<IList<SalesRepTopSeller>> GetTopSellersAsync(SalesRepTopSellerCriteria criteria);

    /// <summary>
    /// The distinct categories the criteria's sales fall into, taken from the line items' own category snapshot. This
    /// is what the category filter is built on rather than the sold products: its cardinality is bounded by the catalog
    /// structure, not by the number of products ever sold.
    /// </summary>
    Task<IList<string>> GetSoldCategoryIdsAsync(SalesRepTopSellerCriteria criteria);
}
