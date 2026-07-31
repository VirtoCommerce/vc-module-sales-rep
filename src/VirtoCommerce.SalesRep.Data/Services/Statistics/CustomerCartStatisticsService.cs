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

        var byCurrency = await AggregateByCurrencyAsync(repository, criteria);
        var itemQuantities = await AggregateItemQuantitiesAsync(repository, criteria);

        var period = await ConvertAndFoldAsync(byCurrency, criteria);
        period.SelectedItemQuantity = itemQuantities.Where(x => x.SelectedForCheckout).Sum(x => x.Quantity);
        period.UnselectedItemQuantity = itemQuantities.Where(x => !x.SelectedForCheckout).Sum(x => x.Quantity);

        return period;
    }

    private async Task<IList<PerCurrencyAggregate>> AggregateByCurrencyAsync(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
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

    private async Task<IList<ItemQuantityAggregate>> AggregateItemQuantitiesAsync(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        // At most two rows (selected / not selected), so quantities need no currency folding — they are unit counts.
        return await BuildItemQuery(repository, criteria)
            .GroupBy(x => x.SelectedForCheckout)
            .Select(g => new ItemQuantityAggregate
            {
                SelectedForCheckout = g.Key,
                Quantity = g.Sum(x => x.Quantity),
            })
            .ToListAsync();
    }

    // The range bounds the item's own modified date, not the cart's created date, so a cart opened months ago still
    // contributes the items touched inside it. Cart scope still comes from BuildQuery — with the cart-level-only
    // predicates cleared — so an override of that seam narrows the item metrics too.
    protected virtual IQueryable<LineItemEntity> BuildItemQuery(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        var itemCriteria = criteria.CloneTyped();
        itemCriteria.FromDate = null;
        itemCriteria.ToDate = null;
        // An empty cart joins no line items anyway, and LineItemsCount is denormalized over non-gift lines only —
        // gating on it would drop a gift-only or stale-counter cart's items.
        itemCriteria.OnlyNonEmpty = false;

        var query = BuildQuery(repository, itemCriteria).SelectMany(x => x.Items);

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
        period.Warning = folded.Warning;
        return period;
    }

    private sealed class PerCurrencyAggregate
    {
        public string Currency { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public DateTime LastCartDate { get; set; }
    }

    private sealed class ItemQuantityAggregate
    {
        public bool SelectedForCheckout { get; set; }
        public int Quantity { get; set; }
    }
}
