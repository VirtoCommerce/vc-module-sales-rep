using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// "Latest order per organization" lookup used by the Sales Rep "My customers" list (VCST-5304). A whole page of
/// customers is resolved through a single call (the <c>lastOrder</c> field batches them with a DataLoader), which
/// then runs one bounded, newest-first search per organization — instead of a resolver-level order query per row.
/// Standalone — it does not extend or replace the platform-wide
/// <see cref="VirtoCommerce.OrdersModule.Core.Services.ICustomerOrderSearchService"/>.
/// </summary>
public interface ISalesRepCustomerOrderSearchService
{
    /// <summary>
    /// Returns the most recent order (by created date) for each of the specified organizations. Organizations
    /// without orders are omitted; prototype orders are excluded. <paramref name="storeId"/> scopes to a single
    /// store (pass <c>null</c> for all stores) — so a rep never sees order metadata from a store outside the
    /// current storefront. <paramref name="responseGroup"/> is the <see cref="CustomerOrderResponseGroup"/> string
    /// controlling how much of each order is hydrated; the caller computes it from the requested fields.
    /// </summary>
    Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string storeId, string responseGroup);
}
