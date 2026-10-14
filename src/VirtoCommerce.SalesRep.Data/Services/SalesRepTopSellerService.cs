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
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.Families.TopSeller,
            GetType(), nameof(GetTopSellersAsync), criteria,
            () => ComputeTopSellersAsync(criteria));
    }

    private async Task<IList<SalesRepTopSeller>> ComputeTopSellersAsync(SalesRepTopSellerCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var query = BuildQuery(repository, criteria);
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        // The ranking runs in the database and only the winners come back: a rep who has sold 100k distinct products
        // would otherwise materialize a row per product (times price/snapshot variants) just to keep the top few.
        var rankedProductIds = await GetRankedProductIdsAsync(query, criteria, currencies);
        if (rankedProductIds.Count == 0)
        {
            return [];
        }

        // Unit price rides in the group key and revenue (price × units) is summed in memory rather than in SQL, so the
        // figures keep going through the shared currency fold (rounding + unconfigured-currency warning). Rows are
        // bounded by the ranked products, whatever the catalog size.
        var aggregates = await query
            .Where(x => rankedProductIds.Contains(x.ProductId))
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
                LastOrderedDate = g.Max(x => x.CustomerOrder.CreatedDate),
            })
            .ToListAsync();

        var productsById = aggregates
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => BuildTopSeller(g.Key, g.ToList(), criteria.CurrencyCode, currencies), StringComparer.Ordinal);

        // GetRankedProductIdsAsync already decided the order; re-sorting here on the folded figures is what let the two
        // passes disagree.
        var top = rankedProductIds
            .Select(productsById.GetValueOrDefault)
            .Where(x => x != null)
            .ToList();

        for (var i = 0; i < top.Count; i++)
        {
            top[i].Rank = i + 1;
        }

        return top;
    }

    /// <summary>
    /// The single place the order is decided: the caller hydrates and renders in exactly this order. Each sort ranks on
    /// its own metric alone, with the product id breaking ties so equal-metric rows come back the same way every call.
    /// Units rank entirely in the database; revenue can't, because no single LINQ expression multiplies the money column
    /// on every provider (PostgreSQL needs a decimal cast that SQLite won't translate), so it ranks in memory over the
    /// narrowest rows — three scalars per (product, currency, unit price), no display strings.
    /// </summary>
    protected virtual async Task<IList<string>> GetRankedProductIdsAsync(
        IQueryable<LineItemEntity> query,
        SalesRepTopSellerCriteria criteria,
        IReadOnlyCollection<Currency> currencies)
    {
        if (criteria.SortBy != SalesRepTopSellerSortBy.Revenue)
        {
            return await query
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Units)
                .ThenBy(x => x.ProductId)
                .Select(x => x.ProductId)
                .Take(criteria.Take)
                .ToListAsync();
        }

        var priceGroups = await query
            .GroupBy(x => new { x.ProductId, x.Currency, x.Price })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Currency,
                g.Key.Price,
                Units = g.Sum(x => x.Quantity),
            })
            .ToListAsync();

        // Resolved once per currency, not per row: the rates are constant and this runs over every
        // (product, currency, unit price) group. An unconfigured currency rates to zero — the same exclusion the fold
        // applies (and reports as a warning); its units still count.
        var rates = priceGroups
            .Select(x => x.Currency ?? string.Empty)
            .DistinctIgnoreCase()
            .ToDictionary(x => x, x => StatisticsCurrencyConverter.GetRate(x, criteria.CurrencyCode, currencies), StringComparer.OrdinalIgnoreCase);

        return priceGroups
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Revenue = g.Sum(x => x.Price * x.Units * rates[x.Currency ?? string.Empty]),
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.ProductId)
            .Select(x => x.ProductId)
            .Take(criteria.Take)
            .ToList();
    }

    public virtual async Task<IList<string>> GetSoldCategoryIdsAsync(SalesRepScopeCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        return await StatisticsCache.GetOrCreateAsync(
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.Families.TopSeller,
            GetType(), nameof(GetSoldCategoryIdsAsync), criteria,
            () => ComputeSoldCategoryIdsAsync(criteria));
    }

    private async Task<IList<string>> ComputeSoldCategoryIdsAsync(SalesRepScopeCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var categoryIds = await BuildQuery(repository, criteria)
            .Select(x => x.CategoryId)
            .Distinct()
            .ToListAsync();

        // A product filed directly under a catalog root has no category, so its line items carry none — they belong to
        // no top-level category either way.
        return categoryIds
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();
    }

    protected virtual IQueryable<LineItemEntity> BuildQuery(IOrderRepository repository, SalesRepTopSellerCriteria criteria)
    {
        var query = repository.LineItems.ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId);

        if (!criteria.CategoryIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.CategoryIds.Contains(x.CategoryId));
        }

        return query.ApplyPeriod(criteria.FromDate, criteria.ToDate);
    }

    protected virtual IQueryable<LineItemEntity> BuildQuery(IOrderRepository repository, SalesRepScopeCriteria criteria)
        => repository.LineItems
            .ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId)
            .ApplyPeriod(criteria.FromDate, criteria.ToDate);

    private SalesRepTopSeller BuildTopSeller(string productId, IList<ProductPriceAggregate> rows, string currencyCode, IReadOnlyCollection<Currency> currencies)
    {
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

        // Display fields are per-order line-item snapshots that can vary; pick the latest deterministically (ordinal
        // tiebreak keeps it stable when order dates tie) rather than an arbitrary GroupBy row.
        var sample = rows
            .OrderByDescending(x => x.LastOrderedDate)
            .ThenBy(x => x.Sku, StringComparer.Ordinal)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.ImageUrl, StringComparer.Ordinal)
            .ThenBy(x => x.CategoryId, StringComparer.Ordinal)
            .First();
        var result = AbstractTypeFactory<SalesRepTopSeller>.TryCreateInstance();
        result.ProductId = productId;
        result.Units = rows.Sum(x => x.Units);
        result.Revenue = folded.Total;
        result.CurrencyCode = folded.CurrencyCode;
        result.Name = sample.Name;
        result.Sku = sample.Sku;
        result.ImageUrl = sample.ImageUrl;
        result.CategoryId = sample.CategoryId;
        result.Warning = folded.Warning;
        return result;
    }

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
        public DateTime LastOrderedDate { get; set; }
    }
}
