using System;
using System.Collections.Generic;
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
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepOrderStatusService : ISalesRepOrderStatusService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;

    public SalesRepOrderStatusService(
        Func<IOrderRepository> orderRepositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
    }

    public virtual async Task<IList<string>> GetUsedStatusesAsync(SalesRepOrderStatusCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // No served organization = nothing in scope. Falling back to a store-wide vocabulary here would offer statuses
        // the caller has no orders for (and leak which statuses other reps' orders use).
        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        // A DISTINCT over the scoped orders is too heavy to run per request, and the vocabulary changes only when a
        // status is used for the first time — so it rides the order-statistics TTL (same source, same staleness).
        return await StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.OrderStatisticsCacheExpiration,
            GetType(), nameof(GetUsedStatusesAsync), criteria.GetCacheKey(),
            () => ComputeUsedStatusesAsync(criteria));
    }

    protected virtual async Task<IList<string>> ComputeUsedStatusesAsync(SalesRepOrderStatusCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var statuses = await BuildQuery(repository, criteria)
            .Select(x => x.Status)
            .Distinct()
            .ToListAsync();

        return statuses
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
    }

    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, SalesRepOrderStatusCriteria criteria)
    {
        // The same scope the orders list applies (CustomerOrderSearchService excludes prototypes; the sales-rep
        // handlers add the served organizations and the rep as the creator), so every offered status has at least one
        // listed order behind it. Cancelled orders are NOT excluded — the list shows them, so "Cancelled" is a
        // legitimate filter.
        var query = repository.CustomerOrders.Where(x => !x.IsPrototype);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        return query;
    }
}
