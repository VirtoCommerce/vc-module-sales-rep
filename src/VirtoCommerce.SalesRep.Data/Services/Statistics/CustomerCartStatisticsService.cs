using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Aggregates a Sales Rep's carts/projects for the dashboard "Active Projects" / cart widgets. Like the order
/// statistics service, sums/counts have no public aggregation API, so this reads the Cart EF store
/// (<see cref="ICartRepository"/>) directly to run DB-side SUM/COUNT/MAX instead of loading carts into memory. That
/// direct Cart.Data dependency is the same deliberate, scoped exception to the module's "reference other modules'
/// .Core, not .Data" rule already made for Orders.Data, justified by this being an analytics query.
/// </summary>
public class CustomerCartStatisticsService : ICustomerCartStatisticsService
{
    private readonly Func<ICartRepository> _cartRepositoryFactory;
    private readonly ICurrencyService _currencyService;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<CustomerCartStatisticsService> _logger;

    public CustomerCartStatisticsService(
        Func<ICartRepository> cartRepositoryFactory,
        ICurrencyService currencyService,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        ILogger<CustomerCartStatisticsService> logger)
    {
        _cartRepositoryFactory = cartRepositoryFactory;
        _currencyService = currencyService;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public virtual Task<CustomerCartStatisticsPeriod> GetStatisticsAsync(CustomerCartStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        return StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.CartStatisticsCacheExpiration,
            GetType(), nameof(GetStatisticsAsync), criteria.GetCacheKey(),
            () => ComputeStatisticsAsync(criteria));
    }

    private async Task<CustomerCartStatisticsPeriod> ComputeStatisticsAsync(CustomerCartStatisticsCriteria criteria)
    {
        var byCurrency = await AggregateByCurrencyAsync(criteria);
        return await ConvertAndFoldAsync(byCurrency, criteria);
    }

    /// <summary>
    /// One grouped-by-currency aggregate query. Returns a raw per-currency sum/count/max — no cart rows are
    /// materialized. Soft-deleted carts are always excluded.
    /// </summary>
    private async Task<IList<PerCurrencyAggregate>> AggregateByCurrencyAsync(CustomerCartStatisticsCriteria criteria)
    {
        using var repository = _cartRepositoryFactory();

        return await BuildQuery(repository, criteria)
            .GroupBy(x => x.Currency)
            .Select(g => new PerCurrencyAggregate
            {
                Currency = g.Key,
                Total = g.Sum(x => x.Total),
                Count = g.Count(),
                LastCartDate = g.Max(x => x.CreatedDate),
            })
            .ToListAsync();
    }

    /// <summary>
    /// The scoped cart query the aggregate runs over: never soft-deleted carts, then the criteria's organization /
    /// creator / store scope, cart type / exclude-type / status filters, the non-empty flag, and the inclusive
    /// created-date range. The extension seam mirroring the order-statistics/counts services — a project subclasses
    /// this service, calls <c>base</c>, and adds its own predicate for a cart-kind rule the standard criteria can't express.
    /// </summary>
    protected virtual IQueryable<ShoppingCartEntity> BuildQuery(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        var query = repository.ShoppingCarts.Where(x => !x.IsDeleted);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only carts created by the calling sales rep — their user id
        // is recorded as the cart's CustomerId (the rep builds the project on the customer's behalf).
        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        if (!criteria.Types.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Types.Contains(x.Type));
        }

        // Exclude blacklisted types (e.g. "Wishlist" projects). Keep null-type carts (the default cart type) — a raw
        // NOT IN would drop them under SQL null semantics, so guard explicitly.
        if (!criteria.ExcludeTypes.IsNullOrEmpty())
        {
            query = query.Where(x => x.Type == null || !criteria.ExcludeTypes.Contains(x.Type));
        }

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        // "Active" carts have contents; LineItemsCount is the persisted line-item count (0 when a cart is emptied,
        // e.g. after its order is placed), so an emptied cart stops counting without loading its items.
        if (criteria.OnlyNonEmpty)
        {
            query = query.Where(x => x.LineItemsCount > 0);
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
    /// Converts each currency group into <see cref="CustomerCartStatisticsCriteria.CurrencyCode"/> and folds the
    /// groups into one period via the shared <see cref="StatisticsCurrencyConverter"/> (current admin-maintained
    /// exchange rates).
    /// </summary>
    private async Task<CustomerCartStatisticsPeriod> ConvertAndFoldAsync(IList<PerCurrencyAggregate> byCurrency, CustomerCartStatisticsCriteria criteria)
    {
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var aggregates = byCurrency.Select(x => new CurrencyStatisticAggregate
        {
            Currency = x.Currency,
            Total = x.Total,
            Count = x.Count,
            LatestDate = x.LastCartDate,
        });

        var folded = StatisticsCurrencyConverter.Fold(aggregates, criteria.CurrencyCode, currencies, _logger);

        var period = AbstractTypeFactory<CustomerCartStatisticsPeriod>.TryCreateInstance();
        period.Total = folded.Total;
        period.Count = folded.Count;
        period.Average = folded.Average;
        period.LastCartDate = folded.LatestDate;
        period.CurrencyCode = folded.CurrencyCode;
        return period;
    }

    /// <summary>Raw per-currency aggregate read from the database, before currency conversion.</summary>
    private sealed class PerCurrencyAggregate
    {
        public string Currency { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public DateTime LastCartDate { get; set; }
    }
}
