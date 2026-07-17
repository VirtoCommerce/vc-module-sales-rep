using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// The Sales Rep module's order search. Extends the Orders module's <see cref="ICustomerOrderSearchService"/> so a
/// single service backs every order read in the module — the orders list uses the inherited
/// <see cref="ICustomerOrderSearchService.SearchAsync"/>, and <see cref="GetLatestOrdersByOrganizationIdsAsync"/>
/// adds the "My customers" latest-order lookup — and a project can override all of it in one place. The default
/// implementation subclasses the Orders search service, reusing its filter and hydration pipeline.
/// </summary>
public interface ISalesRepCustomerOrderSearchService : ICustomerOrderSearchService
{
    /// <summary>
    /// Returns the most recent order (by created date) for each of the specified organizations, resolved in a
    /// single grouped identifiers query for the whole set — not a search per organization. Organizations without
    /// orders are omitted; prototype orders are excluded. <paramref name="customerId"/> restricts the search to
    /// orders created by that user (the sales rep — their user id is the order's <c>CustomerId</c>, as X-Order
    /// scopes its "my orders" list), so each customer's latest order is the rep's own latest order for them, not
    /// the customer's overall latest. <paramref name="storeId"/> scopes to a single store (pass <c>null</c> for
    /// all stores), so a rep never sees order metadata from a store outside the current storefront.
    /// <paramref name="responseGroup"/> is the <see cref="CustomerOrderResponseGroup"/> string controlling how much
    /// of each order is hydrated; the caller computes it from the requested fields.
    /// </summary>
    Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string customerId, string storeId, string responseGroup);
}
