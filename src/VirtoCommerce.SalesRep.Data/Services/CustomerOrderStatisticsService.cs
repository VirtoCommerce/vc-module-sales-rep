using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Aggregates a customer's orders for the Sales Rep profile widgets (VCST-5309). Unlike the "latest order" lookup
/// (which stays on the public order search service), sums/averages have no public aggregation API, so this reads
/// the Orders EF store (<see cref="IOrderRepository"/>) directly to run DB-side SUM/COUNT/MAX instead of loading
/// orders into memory. That direct Orders.Data dependency is a deliberate, scoped exception to the module's
/// "reference other modules' .Core, not .Data" rule, justified by this being an analytics query.
/// </summary>
public class CustomerOrderStatisticsService : ICustomerOrderStatisticsService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<CustomerOrderStatisticsService> _logger;

    public CustomerOrderStatisticsService(
        Func<IOrderRepository> orderRepositoryFactory,
        ICurrencyService currencyService,
        ILogger<CustomerOrderStatisticsService> logger)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
        _currencyService = currencyService;
        _logger = logger;
    }

    public virtual async Task<CustomerOrderStatisticsPeriod> GetStatisticsAsync(CustomerOrderStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var byCurrency = await AggregateByCurrencyAsync(criteria);
        return await ConvertAndFoldAsync(byCurrency, criteria);
    }

    /// <summary>
    /// One grouped-by-currency aggregate query. Returns a raw per-currency sum/count/max — no order rows are
    /// materialized. Cancelled and prototype orders are always excluded.
    /// </summary>
    private async Task<IList<PerCurrencyAggregate>> AggregateByCurrencyAsync(CustomerOrderStatisticsCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var query = repository.CustomerOrders.Where(x => !x.IsPrototype && !x.IsCancelled);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

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
            query = query.Where(x => x.CreatedDate < criteria.ToDate.Value);
        }

        return await query
            .GroupBy(x => x.Currency)
            .Select(g => new PerCurrencyAggregate
            {
                Currency = g.Key,
                Total = g.Sum(x => x.Total),
                Count = g.Count(),
                LastOrderDate = g.Max(x => x.CreatedDate),
            })
            .ToListAsync();
    }

    /// <summary>
    /// Converts each currency group into <see cref="CustomerOrderStatisticsCriteria.CurrencyCode"/> and folds the
    /// groups into one period. Keeping per-currency counts until here is what makes the average correct across a
    /// mix of currencies. Conversion uses current (admin-maintained) exchange rates.
    /// </summary>
    private async Task<CustomerOrderStatisticsPeriod> ConvertAndFoldAsync(IList<PerCurrencyAggregate> byCurrency, CustomerOrderStatisticsCriteria criteria)
    {
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var targetCurrency = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(criteria.CurrencyCode))
            ?? throw new InvalidOperationException($"Currency '{criteria.CurrencyCode}' is not configured; cannot convert order statistics.");

        var period = AbstractTypeFactory<CustomerOrderStatisticsPeriod>.TryCreateInstance();

        var total = 0m;
        var count = 0;
        DateTime? lastOrderDate = null;

        foreach (var group in byCurrency)
        {
            var sourceCurrency = currencies.FirstOrDefault(x => x.Code.EqualsIgnoreCase(group.Currency));
            if (sourceCurrency == null)
            {
                // An order currency with no configured rate can't be converted. Skip it (and flag) rather than
                // blanking the whole widget over one orphan currency; the target currency itself throws above.
                _logger.LogWarning("Skipping {Count} order(s) in unconfigured currency '{Currency}' while computing sales statistics.", group.Count, group.Currency);
                continue;
            }

            // Convert via the domain Money type (the single source of truth for FX rate math) rather than
            // re-deriving amount * source.ExchangeRate / target.ExchangeRate here. InternalAmount keeps the
            // unrounded decimal; the fold is rounded once at the end. Rates are the current, admin-maintained
            // ExchangeRate values (relative to the primary currency).
            total += new Money(group.Total, sourceCurrency).ConvertTo(targetCurrency).InternalAmount;
            count += group.Count;

            if (lastOrderDate == null || group.LastOrderDate > lastOrderDate)
            {
                lastOrderDate = group.LastOrderDate;
            }
        }

        period.Total = Math.Round(total, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);
        period.Count = count;
        period.Average = count == 0
            ? 0m
            : Math.Round(total / count, targetCurrency.DecimalDigits, MidpointRounding.AwayFromZero);
        period.LastOrderDate = lastOrderDate;

        return period;
    }

    /// <summary>Raw per-currency aggregate read from the database, before currency conversion.</summary>
    private sealed class PerCurrencyAggregate
    {
        public string Currency { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
        public DateTime LastOrderDate { get; set; }
    }
}
