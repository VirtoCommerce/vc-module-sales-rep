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
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>
/// A customer's order statistics in one currency (VCST-5309). Request any number of ranges via aliased
/// <c>period(from, to)</c> selections and any number of <c>comparison(current, previous)</c> selections; a
/// per-range DataLoader coalesces them so each distinct range (and status filter) is aggregated only once per
/// request, and a range shared between a <c>period</c> and a <c>comparison</c> is not queried twice.
/// Each <c>period</c>/<c>comparison</c> also takes an optional <c>statuses</c> filter (business-status names, e.g.
/// "New" or "OnHold") so status-scoped widgets ("New Orders", "Orders on Hold") reuse this one query.
/// </summary>
public class CustomerOrderStatisticsType : ExtendableGraphType<CustomerOrderStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerOrderStatisticsService _statisticsService;
    private readonly ISalesRepOrderStatusService _statusService;

    public CustomerOrderStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerOrderStatisticsService statisticsService,
        ISalesRepOrderStatusService statusService)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;
        _statusService = statusService;

        Name = "CustomerOrderStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency all figures below are converted to.");

        Field<CustomerOrderStatisticsPeriodType>("period")
            .Description("Order statistics for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>("from", "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>("to", "Exclusive upper bound on the order created date (null = no upper bound).")
            .Argument<ListGraphType<StringGraphType>>("statuses", "Optional business-status names (salesRepOrderStatuses 'name's); counts only orders whose status is in the union those names resolve to. Omit for every status.")
            .ResolveAsync(async context =>
            {
                var from = context.GetArgument<DateTime?>("from");
                var to = context.GetArgument<DateTime?>("to");

                var (statuses, blocked) = await ResolveStatusFilterAsync(context);
                if (blocked)
                {
                    return EmptyPeriod(context);
                }

                return GetPeriodLoader(context).LoadAsync((from, to, StatisticsFieldHelper.EncodeSet(statuses)));
            });

        Field<CustomerOrderStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a range shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("current", "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("previous", "The baseline period to compare against.")
            .Argument<ListGraphType<StringGraphType>>("statuses", "Optional business-status names applied to both periods (see 'period.statuses').")
            .ResolveAsync(async context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>("current");
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>("previous");

                var (statuses, blocked) = await ResolveStatusFilterAsync(context);
                if (blocked)
                {
                    return EmptyComparison(context);
                }

                var loader = GetPeriodLoader(context);
                var statusesKey = StatisticsFieldHelper.EncodeSet(statuses);

                // Queue both loads before chaining so they land in the same batch (one dispatch); the two ranges
                // are independent, so deferring 'previous' into 'current's continuation would force a second
                // round-trip whenever it isn't already requested as a sibling 'period'.
                var currentResult = loader.LoadAsync((current.From, current.To, statusesKey));
                var previousResult = loader.LoadAsync((previous.From, previous.To, statusesKey));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    /// <summary>
    /// Resolves the field's <c>statuses</c> argument (business-status names) to the underlying order statuses to
    /// filter by, via the shared <see cref="ISalesRepOrderStatusService"/> (the same 1:many mapping the orders list
    /// uses). Returns <c>(null, false)</c> when no status filter was requested. Fail-closed: when names were given
    /// but resolve to nothing (all unrecognized for this store), returns <c>(null, true)</c> so the caller yields
    /// zeros rather than silently dropping the filter and counting every order — mirroring the orders-list behavior.
    /// </summary>
    private async Task<(string[] Statuses, bool Blocked)> ResolveStatusFilterAsync(IResolveFieldContext context)
    {
        var statusNames = context.GetArgument<string[]>("statuses");
        if (statusNames == null || statusNames.Length == 0)
        {
            return (null, false);
        }

        var statisticsContext = (CustomerOrderStatisticsContext)context.Source;
        var resolved = await _statusService.ResolveOrderStatusesAsync(statisticsContext.StoreId, statusNames);
        return resolved.Length == 0 ? (null, true) : (resolved, false);
    }

    // A per-request batch loader shared by 'period' and 'comparison'. Keyed on the shared context (rep, organizations,
    // store, currency) so every distinct (range, status-filter) under one 'statistics' node is aggregated exactly once.
    private IDataLoader<(DateTime? From, DateTime? To, string Statuses), CustomerOrderStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerOrderStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerOrderStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Statuses), CustomerOrderStatisticsPeriod>(
            loaderKey,
            async ranges =>
            {
                // Each distinct (range, status-filter) is one aggregate query; they run concurrently, each on its own
                // repository instance (its own DbContext), so parallel access is safe.
                var tasks = ranges.Select(async range =>
                {
                    var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.Statuses = StatisticsFieldHelper.DecodeSet(range.Statuses);
                    criteria.FromDate = range.From;
                    criteria.ToDate = range.To;

                    var period = await _statisticsService.GetStatisticsAsync(criteria);
                    return (range, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.range, x => x.period);
            });
    }

    private static CustomerOrderStatisticsPeriod EmptyPeriod(IResolveFieldContext context)
    {
        var period = AbstractTypeFactory<CustomerOrderStatisticsPeriod>.TryCreateInstance();
        period.CurrencyCode = ((CustomerOrderStatisticsContext)context.Source).CurrencyCode;
        return period;
    }

    // Fail-closed comparison: both sides zero (BuildComparison(empty, empty) → zero changes, null percents).
    private static CustomerOrderStatisticsComparison EmptyComparison(IResolveFieldContext context)
    {
        var empty = EmptyPeriod(context);
        return BuildComparison(empty, empty);
    }

    private static CustomerOrderStatisticsComparison BuildComparison(CustomerOrderStatisticsPeriod current, CustomerOrderStatisticsPeriod previous)
    {
        var result = AbstractTypeFactory<CustomerOrderStatisticsComparison>.TryCreateInstance();

        // Both periods are converted to the same target currency, so the change values carry that currency too.
        result.CurrencyCode = current.CurrencyCode;
        result.TotalChange = current.Total - previous.Total;
        result.TotalChangePercent = StatisticsFieldHelper.Percent(previous.Total, current.Total);
        result.CountChange = current.Count - previous.Count;
        result.CountChangePercent = StatisticsFieldHelper.Percent(previous.Count, current.Count);
        result.AverageChange = current.Average - previous.Average;
        result.AverageChangePercent = StatisticsFieldHelper.Percent(previous.Average, current.Average);

        return result;
    }
}
