using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Resolves the most recent order per organization with a single grouped query, for the Sales Rep
/// "My customers" list. Talks directly to the order repository; it is standalone and registered under
/// <see cref="ISalesRepCustomerOrderSearchService"/> only, so the platform-wide
/// <see cref="VirtoCommerce.OrdersModule.Core.Services.ICustomerOrderSearchService"/> registration is unaffected.
/// </summary>
public class SalesRepCustomerOrderSearchService : ISalesRepCustomerOrderSearchService
{
    private readonly Func<IOrderRepository> _repositoryFactory;

    public SalesRepCustomerOrderSearchService(Func<IOrderRepository> repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public virtual async Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string storeId = null)
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

        using var repository = _repositoryFactory();

        // Resolve the id of the most recent order per organization with a single grouped query (top row per group),
        // avoiding one query per organization. The Id tiebreaker keeps ties (same CreatedDate) deterministic.
        // When a store is supplied, scope to it so a rep never sees another store's orders.
        var latestOrderIds = await repository.CustomerOrders
            .Where(x => !x.IsPrototype
                && organizationIdsToSearch.Contains(x.OrganizationId)
                && (storeId == null || x.StoreId == storeId))
            .GroupBy(x => x.OrganizationId)
            .Select(g => g
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefault())
            .ToListAsync();

        if (latestOrderIds.Count == 0)
        {
            return result;
        }

        // WithPrices so the grand total is populated: OrderRepository.GetCustomerOrdersByIdsAsync calls
        // ResetPrices() (zeroing Total) whenever WithPrices is absent. WithPrices only gates that reset and
        // loads no child collections — unlike Full, which also pulls items/payments/shipments/refunds/etc.
        // that the 6-scalar SalesRepLastOrder never uses (and this runs per page through the batch loader).
        var entities = await repository.GetCustomerOrdersByIdsAsync(latestOrderIds, CustomerOrderResponseGroup.WithPrices.ToString());

        foreach (var entity in entities)
        {
            var order = entity.ToModel(AbstractTypeFactory<CustomerOrder>.TryCreateInstance());
            if (!string.IsNullOrEmpty(order.OrganizationId))
            {
                result[order.OrganizationId] = order;
            }
        }

        return result;
    }
}
