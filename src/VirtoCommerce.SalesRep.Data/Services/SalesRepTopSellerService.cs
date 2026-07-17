using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Ranks the products a Sales Rep sold (VCST-5309, "Top Sellers"). Reads the Orders EF store
/// (<see cref="IOrderRepository.LineItems"/>) directly — the same scoped Orders.Data exception the statistics
/// services use — and aggregates DB-side (<c>GROUP BY</c> product + currency, <c>SUM</c> of units and of price ×
/// quantity), so the query returns one row per product/currency instead of every line item (the rep's order volume
/// can be very large). Per-currency revenue is then folded to the target currency in memory — an exact-decimal,
/// provider-independent fold via the shared <see cref="StatisticsCurrencyConverter"/>. The row's display data is the
/// line-item snapshot, so no catalog read.
/// </summary>
public class SalesRepTopSellerService : ISalesRepTopSellerService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<SalesRepTopSellerService> _logger;

    public SalesRepTopSellerService(
        Func<IOrderRepository> orderRepositoryFactory,
        ICurrencyService currencyService,
        ILogger<SalesRepTopSellerService> logger)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _currencyService = currencyService;
        _logger = logger;
    }

    public virtual async Task<IList<SalesRepTopSeller>> GetTopSellersAsync(SalesRepTopSellerCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = _orderRepositoryFactory();

        // Aggregate DB-side: collapse the scoped line items (the rep's order volume can be very large) to one row per
        // product + currency (+ display snapshot) with SUM(quantity) and SUM(price × quantity). Grouping by the
        // display columns too keeps the projection free of string aggregates (portable across EF providers); a
        // product whose snapshot changed between orders yields more than one row and is re-merged in memory below.
        var aggregates = await BuildQuery(repository, criteria)
            .GroupBy(x => new { x.ProductId, x.Currency, x.Name, x.Sku, x.ImageUrl, x.CategoryId })
            .Select(g => new ProductCurrencyAggregate
            {
                ProductId = g.Key.ProductId,
                Currency = g.Key.Currency,
                Name = g.Key.Name,
                Sku = g.Key.Sku,
                ImageUrl = g.Key.ImageUrl,
                CategoryId = g.Key.CategoryId,
                Units = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Price * x.Quantity),
            })
            .ToListAsync();

        if (aggregates.Count == 0)
        {
            return [];
        }

        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        // Re-merge per product across currencies (and any display-snapshot variants), folding revenue to the target.
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

    /// <summary>
    /// The scoped line-item query the ranking runs over: never cancelled line items or cancelled/prototype orders,
    /// then the criteria's organization/creator/store/category scope and date range (all applied via the line
    /// item's <c>CustomerOrder</c> navigation). Override to customize the set.
    /// </summary>
    protected virtual IQueryable<LineItemEntity> BuildQuery(IOrderRepository repository, SalesRepTopSellerCriteria criteria)
    {
        var query = repository.LineItems.Where(x =>
            !x.IsCancelled &&
            !x.CustomerOrder.IsCancelled &&
            !x.CustomerOrder.IsPrototype);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.CustomerOrder.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only line items of orders the calling rep created.
        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerOrder.CustomerId == criteria.CustomerId);
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.CustomerOrder.StoreId == criteria.StoreId);
        }

        if (!criteria.CategoryIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.CategoryIds.Contains(x.CategoryId));
        }

        if (criteria.FromDate != null)
        {
            query = query.Where(x => x.CustomerOrder.CreatedDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            query = query.Where(x => x.CustomerOrder.CreatedDate < criteria.ToDate.Value);
        }

        return query;
    }

    private SalesRepTopSeller BuildTopSeller(string productId, IList<ProductCurrencyAggregate> rows, string currencyCode, IReadOnlyCollection<Currency> currencies)
    {
        // Revenue is summed per currency then folded to the target; units are currency-independent.
        var byCurrency = rows
            .GroupBy(x => x.Currency)
            .Select(cg => new CurrencyStatisticAggregate
            {
                Currency = cg.Key,
                Total = cg.Sum(x => x.Revenue),
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

    // One DB-side aggregate row: a product's summed units and revenue in a single currency (and display snapshot).
    private sealed class ProductCurrencyAggregate
    {
        public string ProductId { get; set; }
        public string Currency { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public string ImageUrl { get; set; }
        public string CategoryId { get; set; }
        public int Units { get; set; }
        public decimal Revenue { get; set; }
    }
}
