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
    /// materialized. The set of orders is shaped by <see cref="BuildQuery"/>.
    /// </summary>
    private async Task<IList<PerCurrencyAggregate>> AggregateByCurrencyAsync(CustomerOrderStatisticsCriteria criteria)
    {
        using var repository = _orderRepositoryFactory();

        var query = BuildQuery(repository, criteria);

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
    /// Builds the filtered order query the aggregate runs over: always excludes cancelled/prototype orders, then
    /// applies the criteria's organization/creator/store scope, status filter and date range. The extension seam
    /// for the "restrict to shared-expressible" escape — a project that needs a rule the standard criteria can't
    /// express (e.g. a "trashed" rule = new-and-stale OR item-less) subclasses this service, calls <c>base</c>, and
    /// adds its own predicate when it recognizes a flag on its own <see cref="CustomerOrderStatisticsCriteria"/>
    /// subclass. Keep it consistent with the orders-list reader (see the reconciliation test).
    /// </summary>
    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, CustomerOrderStatisticsCriteria criteria)
    {
        var query = repository.CustomerOrders.Where(x => !x.IsPrototype && !x.IsCancelled);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only orders created by the calling sales rep — their user id
        // is recorded as the order's CustomerId, exactly as X-Order scopes "my orders".
        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
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

        return query;
    }

    /// <summary>
    /// Converts each currency group into <see cref="CustomerOrderStatisticsCriteria.CurrencyCode"/> and folds the
    /// groups into one period via the shared <see cref="StatisticsCurrencyConverter"/> (current admin-maintained
    /// exchange rates). Keeping per-currency counts until the fold is what makes the average correct across a mix
    /// of currencies.
    /// </summary>
    private async Task<CustomerOrderStatisticsPeriod> ConvertAndFoldAsync(IList<PerCurrencyAggregate> byCurrency, CustomerOrderStatisticsCriteria criteria)
    {
        var currencies = (await _currencyService.GetAllCurrenciesAsync()).ToList();

        var aggregates = byCurrency.Select(x => new CurrencyStatisticAggregate
        {
            Currency = x.Currency,
            Total = x.Total,
            Count = x.Count,
            LatestDate = x.LastOrderDate,
        });

        var folded = StatisticsCurrencyConverter.Fold(aggregates, criteria.CurrencyCode, currencies, _logger);

        var period = AbstractTypeFactory<CustomerOrderStatisticsPeriod>.TryCreateInstance();
        period.Total = folded.Total;
        period.Count = folded.Count;
        period.Average = folded.Average;
        period.LastOrderDate = folded.LatestDate;
        period.CurrencyCode = folded.CurrencyCode;
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
