using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

public class SalesRepCustomerCountsService : ISalesRepCustomerCountsService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;

    public SalesRepCustomerCountsService(
        Func<IOrderRepository> orderRepositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
    }

    public virtual Task<SalesRepCustomerCountsPeriod> GetCountsAsync(SalesRepCustomerCountsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.Families.CustomerCounts,
            GetType(), nameof(GetCountsAsync), criteria,
            () => ComputeCountsAsync(criteria));
    }

    private async Task<SalesRepCustomerCountsPeriod> ComputeCountsAsync(SalesRepCustomerCountsCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var scoped = BuildQuery(repository, criteria);

        var inRange = scoped;
        if (criteria.FromDate != null)
        {
            inRange = inRange.Where(x => x.CreatedDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            inRange = inRange.Where(x => x.CreatedDate <= criteria.ToDate.Value);
        }

        var orderingCustomers = await inRange
            .Select(x => x.OrganizationId)
            .Distinct()
            .CountAsync();

        var assignmentDates = criteria.AssignmentDates ?? [];
        var newCustomers = assignmentDates.Count(assigned =>
            (criteria.FromDate == null || assigned >= criteria.FromDate.Value) &&
            (criteria.ToDate == null || assigned <= criteria.ToDate.Value));

        var result = AbstractTypeFactory<SalesRepCustomerCountsPeriod>.TryCreateInstance();
        result.OrderingCustomers = orderingCustomers;
        result.NewCustomers = newCustomers;
        return result;
    }

    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, SalesRepCustomerCountsCriteria criteria)
    {
        return repository.CustomerOrders.ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId);
    }
}
