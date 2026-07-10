using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Resolves the most recent order per organization for the Sales Rep "My customers" list. It goes through the
/// Orders module's public <see cref="ICustomerOrderSearchService"/> — one bounded, newest-first <c>Take = 1</c>
/// search per organization — rather than the Orders EF repository, so this module stays decoupled from the Orders
/// data layer. Registered under <see cref="ISalesRepCustomerOrderSearchService"/> only.
/// </summary>
public class SalesRepCustomerOrderSearchService : ISalesRepCustomerOrderSearchService
{
    private readonly ICustomerOrderSearchService _customerOrderSearchService;

    public SalesRepCustomerOrderSearchService(ICustomerOrderSearchService customerOrderSearchService)
    {
        _customerOrderSearchService = customerOrderSearchService;
    }

    public virtual async Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string storeId, string responseGroup)
    {
        var result = new Dictionary<string, CustomerOrder>(StringComparer.OrdinalIgnoreCase);

        var organizationIdsToSearch = organizationIds?
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray() ?? [];

        // One bounded "newest order" search per organization through the Orders module's public search service.
        // (There is no public grouped "latest per organization" query, and reaching into the Orders EF repository
        // would couple this module to Orders' data layer.)
        foreach (var organizationId in organizationIdsToSearch)
        {
            var order = await GetLatestOrderAsync(organizationId, storeId, responseGroup);
            if (order != null)
            {
                result[organizationId] = order;
            }
        }

        return result;
    }

    protected virtual async Task<CustomerOrder> GetLatestOrderAsync(string organizationId, string storeId, string responseGroup)
    {
        var criteria = AbstractTypeFactory<CustomerOrderSearchCriteria>.TryCreateInstance();
        criteria.OrganizationIds = [organizationId];
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(storeId) ? null : [storeId];
        criteria.Sort = "createdDate:desc";
        criteria.Take = 1;
        // The caller computes the response group from the requested GraphQL fields — load only what's needed
        // (e.g. WithPrices for total, WithItems for items count). Prototypes are excluded by default
        // (CustomerOrderSearchCriteria.WithPrototypes = false).
        criteria.ResponseGroup = responseGroup;

        var searchResult = await _customerOrderSearchService.SearchAsync(criteria);
        return searchResult.Results.FirstOrDefault();
    }
}
