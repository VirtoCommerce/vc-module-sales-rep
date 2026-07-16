using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// The Sales Rep module's order search. Subclasses the Orders module's <see cref="CustomerOrderSearchService"/> so
/// every order read in the module runs through one service: the orders list uses the inherited
/// <see cref="CustomerOrderSearchService.SearchAsync"/>, and <see cref="GetLatestOrdersByOrganizationIdsAsync"/>
/// adds a grouped "latest order per organization" lookup for the "My customers" list. Subclassing — rather than
/// composing <see cref="ICustomerOrderSearchService"/> and issuing a search per organization — lets the grouped
/// query reuse the base filter pipeline (<c>BuildQuery</c>) and id→model hydration (<c>CreateSearchResultAsync</c>)
/// without duplicating them or reaching into the Orders EF model directly.
/// </summary>
public class SalesRepCustomerOrderSearchService : CustomerOrderSearchService, ISalesRepCustomerOrderSearchService
{
    private readonly Func<IOrderRepository> _repositoryFactory;
    private readonly ICustomerOrderService _customerOrderService;

    public SalesRepCustomerOrderSearchService(
        Func<IOrderRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        ICustomerOrderService crudService,
        IOptions<CrudOptions> crudOptions)
        : base(repositoryFactory, platformMemoryCache, crudService, crudOptions)
    {
        _repositoryFactory = repositoryFactory;
        _customerOrderService = crudService;
    }

    public virtual async Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string customerId, string storeId, string responseGroup)
    {
        var result = new Dictionary<string, CustomerOrder>(StringComparer.OrdinalIgnoreCase);

        var organizationIdsToSearch = organizationIds?
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray() ?? [];

        if (organizationIdsToSearch.Length == 0)
        {
            return result;
        }

        var criteria = AbstractTypeFactory<CustomerOrderSearchCriteria>.TryCreateInstance();
        criteria.OrganizationIds = organizationIdsToSearch;
        // Only orders created by this sales rep — their user id is the order's CustomerId (as X-Order scopes its
        // "my orders" list), so each customer's "last order" is the rep's own latest order for them, not the
        // customer's overall latest.
        criteria.CustomerId = customerId;
        // Scope to the caller's store when provided so a rep never sees another store's orders.
        criteria.StoreIds = string.IsNullOrEmpty(storeId) ? null : [storeId];
        // The caller computes the response group from the requested GraphQL fields — load only what's needed
        // (e.g. WithPrices for total, WithItems for items count). Prototypes are excluded by BuildQuery's default.
        criteria.ResponseGroup = responseGroup;

        using var repository = _repositoryFactory();

        // Reuse the base filter pipeline (organization / store / prototype conditions), then keep only the newest
        // order per organization with a NOT EXISTS anti-join — "no other matching order in the same organization is
        // newer". One grouped identifiers query for the whole set instead of a search per organization.
        var query = BuildQuery(repository, criteria);

        var latestOrderIds = await query
            .Where(order => !query.Any(other =>
                other.OrganizationId == order.OrganizationId &&
                other.CreatedDate > order.CreatedDate))
            .Select(order => order.Id)
            .ToListAsync();

        if (latestOrderIds.Count == 0)
        {
            return result;
        }

        // Hydrate the selected orders via the Orders crud service, honoring the response group, then key by
        // organization. The indexer tolerates the rare exact-timestamp tie (two orders sharing the newest instant
        // in one organization) by keeping the last — either is an acceptable "latest order".
        var orders = await _customerOrderService.GetAsync(latestOrderIds, responseGroup);

        foreach (var order in orders)
        {
            result[order.OrganizationId] = order;
        }

        return result;
    }
}
