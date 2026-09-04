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

    public virtual async Task<IList<string>> GetUsedStatusesAsync(SalesRepScopeCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // No served organization = nothing in scope. A store-wide fallback would offer statuses the caller has no
        // orders for (and leak which statuses other reps' orders use).
        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        // The DISTINCT is too heavy to run per request and the vocabulary only changes when a status is first used, so
        // it rides the order-statistics TTL.
        return await StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.Families.Order,
            GetType(), nameof(GetUsedStatusesAsync), criteria,
            () => ComputeUsedStatusesAsync(criteria));
    }

    private async Task<IList<string>> ComputeUsedStatusesAsync(SalesRepScopeCriteria criteria)
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

    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, SalesRepScopeCriteria criteria)
    {
        // The scope the orders list searches, cancelled orders included: the list shows them, so "Cancelled" is a
        // legitimate filter.
        return repository.CustomerOrders
            .ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId, includeCancelled: true)
            .ApplyPeriod(criteria.FromDate, criteria.ToDate);
    }
}
