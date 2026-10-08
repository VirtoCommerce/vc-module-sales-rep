using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.XFrontend.Core.Statistics.Models;
using VirtoCommerce.XFrontend.Core.Statistics.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

// The order figures are x-frontend's: it aggregates, folds the currencies and caches them per customer. This class
// translates the rep's criteria into that service's and its results back into the sales-rep types.
public class CustomerOrderStatisticsService : ICustomerOrderStatisticsService
{
    private readonly IOrderStatisticsService _orderStatisticsService;

    public CustomerOrderStatisticsService(IOrderStatisticsService orderStatisticsService)
    {
        _orderStatisticsService = orderStatisticsService;
    }

    public virtual async Task<CustomerOrderStatisticsPeriod> GetStatisticsAsync(CustomerOrderStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var statistics = await _orderStatisticsService.GetAsync(ToOrderStatisticsCriteria(criteria));

        return ToPeriod(statistics);
    }

    public virtual async Task<IDictionary<string, CustomerOrderStatisticsPeriod>> GetStatisticsByOrganizationAsync(CustomerOrderStatisticsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var byOrganization = await _orderStatisticsService.GetByOrganizationAsync(ToOrderStatisticsCriteria(criteria));

        return byOrganization.ToDictionary(x => x.Key, x => ToPeriod(x.Value), StringComparer.OrdinalIgnoreCase);
    }

    protected virtual OrderStatisticsCriteria ToOrderStatisticsCriteria(CustomerOrderStatisticsCriteria criteria)
    {
        var result = AbstractTypeFactory<OrderStatisticsCriteria>.TryCreateInstance();

        // The rep's own orders: a rep-placed order records the rep's user id as its CustomerId.
        result.CustomerId = criteria.CustomerId;
        result.OrganizationIds = criteria.OrganizationIds;
        result.StoreId = criteria.StoreId;
        result.Statuses = criteria.Statuses;
        result.FromDate = criteria.FromDate;
        result.ToDate = criteria.ToDate;
        result.CurrencyCode = criteria.CurrencyCode;

        return result;
    }

    protected virtual CustomerOrderStatisticsPeriod ToPeriod(OrderStatistics statistics)
    {
        var period = AbstractTypeFactory<CustomerOrderStatisticsPeriod>.TryCreateInstance();
        period.Total = statistics.Total;
        period.Count = statistics.Count;
        period.Average = statistics.Average;
        period.FirstOrderDate = statistics.FirstOrderDate;
        period.LastOrderDate = statistics.LastOrderDate;
        period.CurrencyCode = statistics.CurrencyCode;
        period.Warning = BuildWarning(statistics);

        return period;
    }

    protected virtual string BuildWarning(OrderStatistics statistics)
    {
        return StatisticsCurrencyConverter.BuildWarning(statistics.ExcludedCount, statistics.ExcludedCurrencies);
    }
}
