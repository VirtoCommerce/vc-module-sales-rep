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

        if (criteria.ResponseGroup.HasFlag(CartStatisticsResponseGroup.ItemQuantities))
        {
            await AddItemQuantitiesAsync(period, itemQuery);
        }

        if (criteria.ResponseGroup.HasFlag(CartStatisticsResponseGroup.CartFigures))
        {
            await AddCartFiguresAsync(period, itemQuery, criteria);
        }

        return period;
    }

    private static async Task AddItemQuantitiesAsync(CustomerCartStatisticsPeriod period, IQueryable<LineItemEntity> itemQuery)
    {
        var quantities = await itemQuery
            .GroupBy(x => x.SelectedForCheckout)
            .Select(g => new { SelectedForCheckout = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToListAsync();

        period.SelectedItemQuantity = quantities.Where(x => x.SelectedForCheckout).Sum(x => x.Quantity);
        period.UnselectedItemQuantity = quantities.Where(x => !x.SelectedForCheckout).Sum(x => x.Quantity);
    }

    private async Task AddCartFiguresAsync(
        CustomerCartStatisticsPeriod period,
        IQueryable<LineItemEntity> itemQuery,
        CustomerCartStatisticsCriteria criteria)
    {
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();
        var contributingLines = itemQuery.Where(x => x.SelectedForCheckout && !x.IsGift);

        var priceGroups = await AggregatePriceGroupsAsync(contributingLines);

        var rates = priceGroups
            .Select(x => x.Currency ?? string.Empty)
            .DistinctIgnoreCase()
            .ToDictionary(x => x, x => StatisticsCurrencyConverter.GetRate(x, criteria.CurrencyCode, currencies), StringComparer.OrdinalIgnoreCase);

        // The SQL IN is case-sensitive on PostgreSQL — carry every actual spelling whose rate resolved.
        var convertibleCurrencies = priceGroups
            .Select(x => x.Currency ?? string.Empty)
            .Distinct()
            .Where(x => rates[x] != 0m)
            .ToArray();

        var byCurrency = priceGroups
            .GroupBy(x => x.Currency)
            .Select(g => new CurrencyStatisticAggregate
            {
                Currency = g.Key,
                Total = g.Sum(x => (x.ListPrice - x.DiscountAmount) * x.Quantity),
            });

        var folded = StatisticsCurrencyConverter.Fold(
            byCurrency, criteria.CurrencyCode, currencies, _logger,
            await CountContributingCartsAsync(contributingLines, convertibleCurrencies));

        period.Total = folded.Total;
        period.Count = folded.Count;
        period.Average = folded.Average;
        period.CurrencyCode = folded.CurrencyCode;
        period.Warning = folded.Warning;
    }

    private static async Task<IList<PriceGroup>> AggregatePriceGroupsAsync(IQueryable<LineItemEntity> contributingLines)
    {
        return await contributingLines
            .GroupBy(x => new { x.Currency, x.ListPrice, x.DiscountAmount })
            .Select(g => new PriceGroup
            {
                Currency = g.Key.Currency,
                ListPrice = g.Key.ListPrice,
                DiscountAmount = g.Key.DiscountAmount,
                Quantity = g.Sum(x => x.Quantity < 1 ? 1 : x.Quantity),
            })
            .ToListAsync();
    }

    private static async Task<int> CountContributingCartsAsync(IQueryable<LineItemEntity> contributingLines, string[] convertibleCurrencies)
    {
        if (convertibleCurrencies.Length == 0)
        {
            return 0;
        }

        return await contributingLines
            .Where(x => convertibleCurrencies.Contains(x.Currency))
            .Select(x => x.ShoppingCartId)
            .Distinct()
            .CountAsync();
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

    // Deliberately time-unbounded (the cart set); the FromDate/ToDate window belongs to BuildItemQuery.
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

    private sealed class PriceGroup
    {
        public string Currency { get; set; }
        public decimal ListPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public int Quantity { get; set; }
    }
}
