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
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Ranks the products a Sales Rep sold (VCST-5309, "Top Sellers"). Reads the Orders EF store
/// (<see cref="IOrderRepository.LineItems"/>) directly — the same scoped Orders.Data exception the statistics
/// services use — and aggregates DB-side (<c>GROUP BY</c> product + currency + unit price, <c>SUM</c> of quantity),
/// so the query returns a compact per-product/price row set instead of every line item (the rep's order volume can
/// be very large). Revenue (price × units) and the cross-currency fold are computed in memory via the shared
/// <see cref="StatisticsCurrencyConverter"/> — the unit price rides in the group key rather than being multiplied in
/// SQL, because Price is a PostgreSQL <c>money</c> column and <c>money * money</c> has no operator. The row's display
/// data is the line-item snapshot, so no catalog read.
/// </summary>
public class SalesRepTopSellerService : ISalesRepTopSellerService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly ICurrencyService _currencyService;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<SalesRepTopSellerService> _logger;

    public SalesRepTopSellerService(
        Func<IOrderRepository> orderRepositoryFactory,
        ICurrencyService currencyService,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        ILogger<SalesRepTopSellerService> logger)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _currencyService = currencyService;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
        _logger = logger;
    }

    public virtual async Task<IList<SalesRepTopSeller>> GetTopSellersAsync(SalesRepTopSellerCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        return await StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.TopSellerCacheExpiration,
            GetType(), nameof(GetTopSellersAsync), criteria.GetCacheKey(),
            () => ComputeTopSellersAsync(criteria));
    }

    private async Task<IList<SalesRepTopSeller>> ComputeTopSellersAsync(SalesRepTopSellerCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        // Aggregate DB-side: collapse the scoped line items (the rep's order volume can be very large) to one row per
        // product + currency + unit price (+ display snapshot) with SUM(quantity). We deliberately do NOT compute
        // SUM(price × quantity) in SQL: Price is a PostgreSQL `money` column and EF renders `price * quantity` as
        // `money * money`, for which Postgres has no operator. Instead the unit price rides in the group key and
        // revenue (price × units) is summed in memory below — exact decimal, provider-independent. The snapshot
        // columns are functionally per-product so they add no rows; only distinct unit prices split a product's
        // rows, which are re-merged per product below.
        var aggregates = await BuildQuery(repository, criteria)
            .GroupBy(x => new { x.ProductId, x.Currency, x.Price, x.Name, x.Sku, x.ImageUrl, x.CategoryId })
            .Select(g => new ProductPriceAggregate
            {
                ProductId = g.Key.ProductId,
                Currency = g.Key.Currency,
                Price = g.Key.Price,
                Name = g.Key.Name,
                Sku = g.Key.Sku,
                ImageUrl = g.Key.ImageUrl,
                CategoryId = g.Key.CategoryId,
                Units = g.Sum(x => x.Quantity),
            })
            .ToListAsync();

        if (aggregates.Count == 0)
        {
            return [];
        }

        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        // Re-merge per product across currencies (and unit-price / snapshot variants), computing revenue and folding.
        var products = aggregates
            .GroupBy(x => x.ProductId)
            .Select(g => BuildTopSeller(g.Key, g.ToList(), criteria.CurrencyCode, currencies))
            .ToList();

        // Rank by the requested metric; the other metric then ProductId break ties for a deterministic order.
        var ordered = criteria.SortBy == SalesRepTopSellerSortBy.Revenue
            ? products.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.Units).ThenBy(x => x.ProductId)
            : products.OrderByDescending(x => x.Units).ThenByDescending(x => x.Revenue).ThenBy(x => x.ProductId);

        var top = ordered.Take(criteria.Take).ToList();
        for (var i = 0; i < top.Count; i++)
        {
            top[i].Rank = i + 1;
        }

        return top;
    }

    public virtual async Task<IList<string>> GetSoldProductIdsAsync(SalesRepTopSellerCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        return await StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.TopSellerCacheExpiration,
            GetType(), nameof(GetSoldProductIdsAsync), criteria.GetCacheKey(),
            () => ComputeSoldProductIdsAsync(criteria));
    }

    private async Task<IList<string>> ComputeSoldProductIdsAsync(SalesRepTopSellerCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        // The rep's distinct sold products in the same scope the ranking uses (creator scope included), so the
        // category filter can bound its catalog-index lookup and never enumerate a whole category.
        return await BuildQuery(repository, criteria)
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// The scoped line-item query the ranking runs over: never cancelled line items or cancelled/prototype orders,
    /// then the criteria's organization/creator/store/category scope and date range (all applied via the line
    /// item's <c>CustomerOrder</c> navigation). Override to customize the set.
    /// </summary>
    protected virtual IQueryable<LineItemEntity> BuildQuery(IOrderRepository repository, SalesRepTopSellerCriteria criteria)
    {
        // Shared rep scope (excludes cancelled line items + cancelled/prototype orders, then org/creator/store);
        // category / product / date filters are layered on below.
        var query = repository.LineItems.ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId);

        if (!criteria.CategoryIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.CategoryIds.Contains(x.CategoryId));
        }

        // Product restriction (category filter option (a)): null = no restriction; an empty set matches nothing.
        if (criteria.ProductIds != null)
        {
            query = query.Where(x => criteria.ProductIds.Contains(x.ProductId));
        }

        if (criteria.FromDate != null)
        {
            query = query.Where(x => x.CustomerOrder.CreatedDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            query = query.Where(x => x.CustomerOrder.CreatedDate <= criteria.ToDate.Value);
        }

        return query;
    }

    private SalesRepTopSeller BuildTopSeller(string productId, IList<ProductPriceAggregate> rows, string currencyCode, IReadOnlyCollection<Currency> currencies)
    {
        // Revenue = Σ(unit price × units) per currency (computed here, not in SQL), then folded to the target;
        // units are currency-independent.
        var byCurrency = rows
            .GroupBy(x => x.Currency)
            .Select(cg => new CurrencyStatisticAggregate
            {
                Currency = cg.Key,
                Total = cg.Sum(x => x.Price * x.Units),
                Count = 0,
                LatestDate = null,
            });

        var folded = StatisticsCurrencyConverter.Fold(byCurrency, currencyCode, currencies, _logger);

        var sample = rows[0];
        var result = AbstractTypeFactory<SalesRepTopSeller>.TryCreateInstance();
        result.ProductId = productId;
        result.Units = rows.Sum(x => x.Units);
        result.Revenue = folded.Total;
        result.CurrencyCode = folded.CurrencyCode;
        result.Name = sample.Name;
        result.Sku = sample.Sku;
        result.ImageUrl = sample.ImageUrl;
        result.CategoryId = sample.CategoryId;
        return result;
    }

    // One DB-side aggregate row: units sold of a product at one unit price in one currency (+ display snapshot).
    // Revenue (Price × Units) is computed in memory — not in SQL — because Price is a PostgreSQL `money` column and
    // `money * money` has no operator; the unit price rides in the group key so no SQL money multiplication occurs.
    private sealed class ProductPriceAggregate
    {
        public string ProductId { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public string ImageUrl { get; set; }
        public string CategoryId { get; set; }
        public int Units { get; set; }
    }
}
