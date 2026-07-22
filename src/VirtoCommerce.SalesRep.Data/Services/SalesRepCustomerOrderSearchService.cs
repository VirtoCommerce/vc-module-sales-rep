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
        criteria.CustomerId = customerId;
        criteria.StoreIds = string.IsNullOrEmpty(storeId) ? null : [storeId];
        criteria.ResponseGroup = responseGroup;

        using var repository = _repositoryFactory();

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

        var orders = await _customerOrderService.GetAsync(latestOrderIds, responseGroup);

        foreach (var order in orders)
        {
            result[order.OrganizationId] = order;
        }

        return result;
    }
}
