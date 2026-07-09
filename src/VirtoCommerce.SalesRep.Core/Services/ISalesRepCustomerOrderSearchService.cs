using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Batched "latest order per organization" lookup used by the Sales Rep "My customers" list (VCST-5304), so the
/// last order for a whole page of customers is resolved in one query instead of one query per row. Standalone —
/// it does not extend or replace the platform-wide
/// <see cref="VirtoCommerce.OrdersModule.Core.Services.ICustomerOrderSearchService"/>.
/// </summary>
public interface ISalesRepCustomerOrderSearchService
{
    /// <summary>
    /// Returns the most recent order (by created date) for each of the specified organizations, resolved with a
    /// single grouped database query. Organizations without orders are omitted; prototype orders are excluded.
    /// When <paramref name="storeId"/> is provided, only orders from that store are considered — so a rep never
    /// sees order metadata from a store outside the current storefront.
    /// </summary>
    Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string storeId = null);
}
