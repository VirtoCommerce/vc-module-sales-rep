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
/// services use — projecting the scoped line items (a bounded set: the rep's own orders), then aggregating per
/// product in memory so revenue math is exact across a currency mix (folded via the shared
/// <see cref="StatisticsCurrencyConverter"/>). The row's display data is the line-item snapshot, so no catalog read.
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

        // Project the scoped line items (bounded: the rep's own orders) and aggregate in memory — exact decimal
        // revenue across a currency mix, and no reliance on provider-specific SUM(price * qty) translation.
        var lines = await BuildQuery(repository, criteria)
            .Select(x => new LineItemProjection
            {
                ProductId = x.ProductId,
                Currency = x.Currency,
                Quantity = x.Quantity,
                Price = x.Price,
                Name = x.Name,
                Sku = x.Sku,
                ImageUrl = x.ImageUrl,
                CategoryId = x.CategoryId,
            })
            .ToListAsync();

        if (lines.Count == 0)
        {
            return [];
        }

        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var products = lines
            .GroupBy(x => x.ProductId)
            .Select(g => BuildTopSeller(g.Key, g.ToList(), criteria.CurrencyCode, currencies))
            .ToList();

        // Rank by the requested metric; the other metric then ProductId break ties for a deterministic order.
        var ordered = criteria.SortBy == SalesRepTopSellerSortBy.Revenue
            ? products.OrderByDescending(x => x.Revenue).ThenByDescending(x => x.Units).ThenBy(x => x.ProductId)
            : products.OrderByDescending(x => x.Units).ThenByDescending(x => x.Revenue).ThenBy(x => x.ProductId);

        var take = criteria.Take > 0 ? criteria.Take : 5;
        var top = ordered.Take(take).ToList();
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

    private SalesRepTopSeller BuildTopSeller(string productId, IList<LineItemProjection> lines, string currencyCode, IReadOnlyCollection<Currency> currencies)
    {
        // Revenue is per-currency then folded to the target; units are currency-independent.
        var byCurrency = lines
            .GroupBy(x => x.Currency)
            .Select(cg => new CurrencyStatisticAggregate
            {
                Currency = cg.Key,
                Total = cg.Sum(x => x.Price * x.Quantity),
                Count = 0,
                LatestDate = null,
            });

        var folded = StatisticsCurrencyConverter.Fold(byCurrency, currencyCode, currencies, _logger);

        var sample = lines[0];
        var result = AbstractTypeFactory<SalesRepTopSeller>.TryCreateInstance();
        result.ProductId = productId;
        result.Units = lines.Sum(x => x.Quantity);
        result.Revenue = folded.Total;
        result.CurrencyCode = folded.CurrencyCode;
        result.Name = sample.Name;
        result.Sku = sample.Sku;
        result.ImageUrl = sample.ImageUrl;
        result.CategoryId = sample.CategoryId;
        return result;
    }

    private sealed class LineItemProjection
    {
        public string ProductId { get; set; }
        public string Currency { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public string ImageUrl { get; set; }
        public string CategoryId { get; set; }
    }
}
