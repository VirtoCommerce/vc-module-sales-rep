using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>
/// A customer's order statistics in one currency (VCST-5309). Request any number of ranges via aliased
/// <c>period(from, to)</c> selections and any number of <c>comparison(current, previous)</c> selections; a
/// per-range DataLoader coalesces them so each distinct range is aggregated only once per request, and a range
/// shared between a <c>period</c> and a <c>comparison</c> is not queried twice.
/// </summary>
public class CustomerOrderStatisticsType : ExtendableGraphType<CustomerOrderStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerOrderStatisticsService _statisticsService;

    public CustomerOrderStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerOrderStatisticsService statisticsService)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;

        Name = "CustomerOrderStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency all figures below are converted to.");

        Field<CustomerOrderStatisticsPeriodType>("period")
            .Description("Order statistics for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>("from", "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>("to", "Exclusive upper bound on the order created date (null = no upper bound).")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>("from");
                var to = context.GetArgument<DateTime?>("to");
                return GetPeriodLoader(context).LoadAsync((from, to));
            });

        Field<CustomerOrderStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a range shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<CustomerOrderStatisticsPeriodInputType>>("current", "The later period.")
            .Argument<NonNullGraphType<CustomerOrderStatisticsPeriodInputType>>("previous", "The baseline period to compare against.")
            .Resolve(context =>
            {
                var current = context.GetArgument<CustomerOrderStatisticsPeriodInput>("current");
                var previous = context.GetArgument<CustomerOrderStatisticsPeriodInput>("previous");
                var loader = GetPeriodLoader(context);

                // Queue both loads before chaining so they land in the same batch (one dispatch); the two ranges
                // are independent, so deferring 'previous' into 'current's continuation would force a second
                // round-trip whenever it isn't already requested as a sibling 'period'.
                var currentResult = loader.LoadAsync((current.From, current.To));
                var previousResult = loader.LoadAsync((previous.From, previous.To));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    // A per-range batch loader shared by 'period' and 'comparison'. Keyed on the shared context (organization,
    // store, currency) so every distinct range under one 'statistics' node is aggregated exactly once per request.
    private IDataLoader<(DateTime? From, DateTime? To), CustomerOrderStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerOrderStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerOrderStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To), CustomerOrderStatisticsPeriod>(
            loaderKey,
            async ranges =>
            {
                // Each distinct range is one aggregate query; they run concurrently, each on its own repository
                // instance (its own DbContext), so parallel access is safe.
                var tasks = ranges.Select(async range =>
                {
                    var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.FromDate = range.From;
                    criteria.ToDate = range.To;

                    var period = await _statisticsService.GetStatisticsAsync(criteria);
                    return (range, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.range, x => x.period);
            });
    }

    private static CustomerOrderStatisticsComparison BuildComparison(CustomerOrderStatisticsPeriod current, CustomerOrderStatisticsPeriod previous)
    {
        var result = AbstractTypeFactory<CustomerOrderStatisticsComparison>.TryCreateInstance();

        // Both periods are converted to the same target currency, so the change values carry that currency too.
        result.CurrencyCode = current.CurrencyCode;
        result.TotalChange = current.Total - previous.Total;
        result.TotalChangePercent = Percent(previous.Total, current.Total);
        result.CountChange = current.Count - previous.Count;
        result.CountChangePercent = Percent(previous.Count, current.Count);
        result.AverageChange = current.Average - previous.Average;
        result.AverageChangePercent = Percent(previous.Average, current.Average);

        return result;
    }

    // Percentage change from a baseline; null when the baseline is zero (no meaningful ratio).
    private static decimal? Percent(decimal previous, decimal current)
    {
        return previous == 0m ? null : (current - previous) / previous * 100m;
    }
}
