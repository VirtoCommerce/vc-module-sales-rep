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
        using var repository = _cartRepositoryFactory();

        var itemQuery = BuildItemQuery(repository, criteria);

        var period = AbstractTypeFactory<CustomerCartStatisticsPeriod>.TryCreateInstance();
        period.CurrencyCode = criteria.CurrencyCode;

        var quantities = await itemQuery
            .GroupBy(x => x.SelectedForCheckout)
            .Select(g => new { SelectedForCheckout = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync();

        period.SelectedItemQuantity = quantities.Where(x => x.SelectedForCheckout).Sum(x => x.Quantity);
        period.UnselectedItemQuantity = quantities.Where(x => !x.SelectedForCheckout).Sum(x => x.Quantity);

        if (criteria.IncludeCartFigures)
        {
            await FoldCartFiguresAsync(period, await AggregateCartFigureRowsAsync(itemQuery), criteria);
        }

        return period;
    }

    // Prices are carried out unmultiplied and multiplied in memory: PostgreSQL has no money * money operator, and
    // the decimal cast that fixes it is not translated by SQLite.
    private static async Task<IList<CartFigureRow>> AggregateCartFigureRowsAsync(IQueryable<LineItemEntity> itemQuery)
    {
        return await itemQuery
            .Where(x => x.SelectedForCheckout && !x.IsGift)
            .GroupBy(x => new { x.Currency, x.ShoppingCartId, x.ListPrice, x.DiscountAmount })
            .Select(g => new CartFigureRow
            {
                Currency = g.Key.Currency,
                CartId = g.Key.ShoppingCartId,
                ListPrice = g.Key.ListPrice,
                DiscountAmount = g.Key.DiscountAmount,
                Quantity = g.Sum(x => x.Quantity < 1 ? 1 : x.Quantity),
            })
            .ToListAsync();
    }

    private async Task FoldCartFiguresAsync(
        CustomerCartStatisticsPeriod period,
        IEnumerable<CartFigureRow> rows,
        CustomerCartStatisticsCriteria criteria)
    {
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var byCurrency = rows
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyStatisticAggregate
            {
                Currency = g.Key,
                Total = g.Sum(x => (x.ListPrice - x.DiscountAmount) * x.Quantity),
                Count = g.Select(x => x.CartId).Distinct().Count(),
            });

        var folded = StatisticsCurrencyConverter.Fold(byCurrency, criteria.CurrencyCode, currencies, _logger);

        period.Total = folded.Total;
        period.Count = folded.Count;
        period.Average = folded.Average;
        period.CurrencyCode = folded.CurrencyCode;
        period.Warning = folded.Warning;
    }

    protected virtual IQueryable<LineItemEntity> BuildItemQuery(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        var query = BuildQuery(repository, criteria).SelectMany(x => x.Items);

        if (criteria.FromDate != null)
        {
            query = query.Where(x => (x.ModifiedDate ?? x.CreatedDate) >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            query = query.Where(x => (x.ModifiedDate ?? x.CreatedDate) <= criteria.ToDate.Value);
        }

        return query;
    }

    protected virtual IQueryable<ShoppingCartEntity> BuildQuery(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        var query = repository.ShoppingCarts.Where(x => !x.IsDeleted);

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

        if (!criteria.Names.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Names.Contains(x.Name));
        }

        if (!criteria.Types.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Types.Contains(x.Type));
        }

        if (!criteria.ExcludeTypes.IsNullOrEmpty())
        {
            query = query.Where(x => x.Type == null || !criteria.ExcludeTypes.Contains(x.Type));
        }

        if (!criteria.Statuses.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.Statuses.Contains(x.Status));
        }

        return query;
    }

    private sealed class CartFigureRow
    {
        public string Currency { get; set; }
        public string CartId { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public int Quantity { get; set; }
    }
}
