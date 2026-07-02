using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Extends the platform <see cref="CustomerOrderSearchService"/> with a batched "latest order per organization"
/// lookup. Registered under <see cref="ISalesRepCustomerOrderSearchService"/> only, so the platform-wide
/// <see cref="ICustomerOrderSearchService"/> registration is unaffected.
/// </summary>
public class SalesRepCustomerOrderSearchService : CustomerOrderSearchService, ISalesRepCustomerOrderSearchService
{
    private readonly Func<IOrderRepository> _repositoryFactory;

    public SalesRepCustomerOrderSearchService(
        Func<IOrderRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        ICustomerOrderService crudService,
        IOptions<CrudOptions> crudOptions)
        : base(repositoryFactory, platformMemoryCache, crudService, crudOptions)
    {
        _repositoryFactory = repositoryFactory;
    }

    public virtual async Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds)
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
        var latestOrderIds = await repository.CustomerOrders
            .Where(x => !x.IsPrototype && organizationIdsToSearch.Contains(x.OrganizationId))
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

        var entities = await repository.GetCustomerOrdersByIdsAsync(latestOrderIds, CustomerOrderResponseGroup.Default.ToString());

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
