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

/// <summary>
/// Aggregates a customer's orders for the Sales Rep profile widgets (VCST-5309). Unlike the "latest order" lookup
/// (which stays on the public order search service), sums/averages have no public aggregation API, so this reads
/// the Orders EF store (<see cref="IOrderRepository"/>) directly to run DB-side SUM/COUNT/MAX/MIN instead of loading
/// orders into memory. That direct Orders.Data dependency is a deliberate, scoped exception to the module's
/// "reference other modules' .Core, not .Data" rule, justified by this being an analytics query.
/// </summary>
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

        // Fold each organization's per-currency aggregates into one period, in the requested currency.
        return byOrgCurrency
            .GroupBy(x => x.OrganizationId)
            .ToDictionary(
                g => g.Key,
                g => BuildPeriod(g.Select(x => x.Aggregate).ToList(), criteria.CurrencyCode, currencies),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One grouped-by-currency aggregate query. Returns a raw per-currency sum/count/max/min — no order rows are
    /// materialized. The set of orders is shaped by <see cref="BuildQuery"/>.
    /// </summary>
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

    /// <summary>
    /// One grouped-by-(organization, currency) aggregate query for the whole organization set — the per-organization
    /// counterpart of <see cref="AggregateByCurrencyAsync"/>, so the "My customers" list resolves every visible row's
    /// purchase figures (and its order-derived sort key) in a single query. Projects to a flat anonymous row in SQL,
    /// then maps in memory (EF can't project into the nested aggregate directly).
    /// </summary>
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

    /// <summary>
    /// Builds the filtered order query the aggregate runs over: always excludes cancelled/prototype orders, then
    /// applies the criteria's organization/creator/store scope, status filter and date range. The extension seam
    /// for the "restrict to shared-expressible" escape — a project that needs a rule the standard criteria can't
    /// express (e.g. a "trashed" rule = new-and-stale OR item-less) subclasses this service, calls <c>base</c>, and
    /// adds its own predicate when it recognizes a flag on its own <see cref="CustomerOrderStatisticsCriteria"/>
    /// subclass. Keep it consistent with the orders-list reader (see the reconciliation test).
    /// </summary>
    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, CustomerOrderStatisticsCriteria criteria)
    {
        // Shared creator/organization/store scope (excludes cancelled/prototype); status + date bounds are layered on.
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

    /// <summary>
    /// Converts one set of per-currency aggregates into <paramref name="currencyCode"/> and folds them into a single
    /// period via the shared <see cref="StatisticsCurrencyConverter"/> (current admin-maintained exchange rates).
    /// Keeping per-currency counts until the fold is what makes the average correct across a mix of currencies.
    /// <c>FirstOrderDate</c>/<c>LastOrderDate</c> are the min/max over the same configured currencies the fold sums,
    /// so an order in an unconfigured currency contributes to neither (consistent with <c>Total</c>/<c>Count</c>).
    /// </summary>
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
        return period;
    }

    /// <summary>Raw per-currency aggregate read from the database, before currency conversion.</summary>
    private sealed class PerCurrencyAggregate
    {
        public string Currency { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public DateTime LastOrderDate { get; set; }
        public DateTime FirstOrderDate { get; set; }
    }

    /// <summary>A per-currency aggregate tagged with its organization, for the grouped-by-organization query.</summary>
    private sealed class PerOrganizationCurrencyAggregate
    {
        public string OrganizationId { get; set; }
        public PerCurrencyAggregate Aggregate { get; set; }
    }
}
