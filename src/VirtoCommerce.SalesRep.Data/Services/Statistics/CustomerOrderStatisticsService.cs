using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

public class CustomerOrderStatisticsService : ICustomerOrderStatisticsService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly ICurrencyService _currencyService;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<CustomerOrderStatisticsService> _logger;

    public CustomerOrderStatisticsService(
        Func<IOrderRepository> orderRepositoryFactory,
        ICurrencyService currencyService,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        ILogger<CustomerOrderStatisticsService> logger)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _currencyService = currencyService;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public virtual Task<CustomerOrderStatisticsPeriod> GetStatisticsAsync(CustomerOrderStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.OrderStatisticsCacheExpiration,
            GetType(), nameof(GetStatisticsAsync), criteria.GetCacheKey(),
            () => ComputeStatisticsAsync(criteria));
    }

    private async Task<CustomerOrderStatisticsPeriod> ComputeStatisticsAsync(CustomerOrderStatisticsCriteria criteria)
    {
        var byCurrency = await AggregateByCurrencyAsync(criteria);
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();
        return BuildPeriod(byCurrency, criteria.CurrencyCode, currencies);
    }

    public virtual Task<IDictionary<string, CustomerOrderStatisticsPeriod>> GetStatisticsByOrganizationAsync(CustomerOrderStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.OrderStatisticsCacheExpiration,
            GetType(), nameof(GetStatisticsByOrganizationAsync), criteria.GetCacheKey(),
            () => ComputeStatisticsByOrganizationAsync(criteria));
    }

    private async Task<IDictionary<string, CustomerOrderStatisticsPeriod>> ComputeStatisticsByOrganizationAsync(CustomerOrderStatisticsCriteria criteria)
    {
        var byOrgCurrency = await AggregateByOrganizationAndCurrencyAsync(criteria);
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        return byOrgCurrency
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => BuildPeriod(g.Select(x => x.Aggregate).ToList(), criteria.CurrencyCode, currencies),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IList<PerCurrencyAggregate>> AggregateByCurrencyAsync(CustomerOrderStatisticsCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var query = BuildQuery(repository, criteria);

        return await query
            .GroupBy(x => x.Currency)
            .Select(g => new PerCurrencyAggregate
            {
                Currency = g.Key,
                Total = g.Sum(x => x.Total),
                Count = g.Count(),
                LastOrderDate = g.Max(x => x.CreatedDate),
                FirstOrderDate = g.Min(x => x.CreatedDate),
            })
            .ToListAsync();
    }

    private async Task<IList<PerOrganizationCurrencyAggregate>> AggregateByOrganizationAndCurrencyAsync(CustomerOrderStatisticsCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var query = BuildQuery(repository, criteria);

        var rows = await query
            .GroupBy(x => new { x.OrganizationId, x.Currency })
            .Select(g => new
            {
                g.Key.OrganizationId,
                g.Key.Currency,
                Total = g.Sum(x => x.Total),
                Count = g.Count(),
                LastOrderDate = g.Max(x => x.CreatedDate),
                FirstOrderDate = g.Min(x => x.CreatedDate),
            })
            .ToListAsync();

        return rows
            .Select(x => new PerOrganizationCurrencyAggregate
            {
                OrganizationId = x.OrganizationId,
                Aggregate = new PerCurrencyAggregate
                {
                    Currency = x.Currency,
                    Total = x.Total,
                    Count = x.Count,
                    LastOrderDate = x.LastOrderDate,
                    FirstOrderDate = x.FirstOrderDate,
                },
            })
            .ToList();
    }

    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, CustomerOrderStatisticsCriteria criteria)
    {
        var query = repository.CustomerOrders.ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId);

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        if (criteria.FromDate != null)
        {
            query = query.Where(x => x.CreatedDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            query = query.Where(x => x.CreatedDate <= criteria.ToDate.Value);
        }

        return query;
    }

    private CustomerOrderStatisticsPeriod BuildPeriod(IList<PerCurrencyAggregate> byCurrency, string currencyCode, IReadOnlyCollection<Currency> currencies)
    {
        var aggregates = byCurrency.Select(x => new CurrencyStatisticAggregate
        {
            Currency = x.Currency,
            Total = x.Total,
            Count = x.Count,
            EarliestDate = x.FirstOrderDate,
            LatestDate = x.LastOrderDate,
        });

        var folded = StatisticsCurrencyConverter.Fold(aggregates, currencyCode, currencies, _logger);

        var period = AbstractTypeFactory<CustomerOrderStatisticsPeriod>.TryCreateInstance();
        period.Total = folded.Total;
        period.Count = folded.Count;
        period.Average = folded.Average;
        period.LastOrderDate = folded.LatestDate;
        period.FirstOrderDate = folded.EarliestDate;
        period.CurrencyCode = folded.CurrencyCode;
        period.Warning = folded.Warning;
        return period;
    }

    private sealed class PerCurrencyAggregate
    {
        public string Currency { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public DateTime LastOrderDate { get; set; }
        public DateTime FirstOrderDate { get; set; }
    }

    private sealed class PerOrganizationCurrencyAggregate
    {
        public string OrganizationId { get; set; }
        public PerCurrencyAggregate Aggregate { get; set; }
    }
}
