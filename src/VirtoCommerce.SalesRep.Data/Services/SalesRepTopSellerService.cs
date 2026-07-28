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
            _platformMemoryCache, _settingsManager, ModuleConstants.Settings.Caching.TopSellerCacheExpiration,
            GetType(), nameof(GetTopSellersAsync), criteria.GetCacheKey(),
            () => ComputeTopSellersAsync(criteria));
    }

    private async Task<IList<SalesRepTopSeller>> ComputeTopSellersAsync(SalesRepTopSellerCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        // Unit price rides in the group key and revenue (price × units) is summed in memory below, NOT in SQL: Price
        // is a Postgres `money` column and EF renders `price * quantity` as `money * money`, which Postgres rejects.
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
                LastOrderedDate = g.Max(x => x.CustomerOrder.CreatedDate),
            })
            .ToListAsync();

        if (aggregates.Count == 0)
        {
            return [];
        }

        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var products = aggregates
            .GroupBy(x => x.ProductId)
            .Select(g => BuildTopSeller(g.Key, g.ToList(), criteria.CurrencyCode, currencies))
            .ToList();

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

        return await BuildQuery(repository, criteria)
            .Select(x => x.ProductId)
            .Distinct()
            .ToListAsync();
    }

    protected virtual IQueryable<LineItemEntity> BuildQuery(IOrderRepository repository, SalesRepTopSellerCriteria criteria)
    {
        var query = repository.LineItems.ApplyRepScope(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId);

        if (!criteria.CategoryIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.CategoryIds.Contains(x.CategoryId));
        }

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
