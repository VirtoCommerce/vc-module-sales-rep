using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>
/// A customer's order statistics in one currency (VCST-5309). Request any number of ranges via aliased
/// <c>period(from, to)</c> selections and any number of <c>comparison(current, previous)</c> selections; a
/// per-(range, status-selection) DataLoader coalesces them so each distinct bucket is aggregated only once per
/// request, and a bucket shared between a <c>period</c> and a <c>comparison</c> is not queried twice.
/// Each <c>period</c>/<c>comparison</c> also takes an optional <c>statuses</c> filter (business-status names, e.g.
/// "New" or "OnHold") so status-scoped widgets ("New Orders", "Orders on Hold") reuse this one query. The selected
/// names are resolved and applied (via the shared <see cref="ISalesRepOrderFilterRuleResolver"/>) inside the loader, so
/// this graph type never sees concrete filter fields.
/// </summary>
public class CustomerOrderStatisticsType : ExtendableGraphType<CustomerOrderStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerOrderStatisticsService _statisticsService;
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public CustomerOrderStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerOrderStatisticsService statisticsService,
        ISalesRepOrderFilterRuleResolver filterRuleResolver)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;
        _filterRuleResolver = filterRuleResolver;

        Name = "CustomerOrderStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency all figures below are converted to.");

        Field<CustomerOrderStatisticsPeriodType>("period")
            .Description("Order statistics for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.FromArgument, "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.ToArgument, "Inclusive upper bound on the order created date (null = no upper bound).")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional filter-rule name (a salesRepOrderFilterRules 'name'); counts only orders matching that rule. Omit for all orders in the range.")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>(StatisticsFieldHelper.FromArgument);
                var to = context.GetArgument<DateTime?>(StatisticsFieldHelper.ToArgument);
                return GetPeriodLoader(context).LoadAsync((from, to, StatisticsFieldHelper.GetFilter(context)));
            });

        Field<CustomerOrderStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a bucket shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.CurrentArgument, "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.PreviousArgument, "The baseline period to compare against.")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional filter-rule name applied to both periods (see 'period.filter').")
            .Resolve(context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.CurrentArgument);
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.PreviousArgument);
                var filterKey = StatisticsFieldHelper.GetFilter(context);

                var loader = GetPeriodLoader(context);

                // Queue both loads before chaining so they land in the same batch (one dispatch); the two ranges
                // are independent, so deferring 'previous' into 'current's continuation would force a second
                // round-trip whenever it isn't already requested as a sibling 'period'.
                var currentResult = loader.LoadAsync((current.From, current.To, filterKey));
                var previousResult = loader.LoadAsync((previous.From, previous.To, filterKey));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    // A per-request batch loader shared by 'period' and 'comparison'. Keyed on the shared context (rep, organizations,
    // store, currency); the batch key adds the range and the raw status selection, so every distinct bucket under one
    // node is aggregated exactly once. Status resolution + fail-closed handling happen here, once per distinct bucket.
    private IDataLoader<(DateTime? From, DateTime? To, string Filter), CustomerOrderStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerOrderStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerOrderStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Filter), CustomerOrderStatisticsPeriod>(
            loaderKey,
            async buckets =>
            {
                // Each distinct (range, status-selection) is one aggregate query; they run concurrently, each on its
                // own repository instance (its own DbContext), so parallel access is safe.
                var tasks = buckets.Select(async bucket =>
                {
                    var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.FromDate = bucket.From;
                    criteria.ToDate = bucket.To;

                    // Apply the selected rule through the shared resolver (same mapping as the orders list). Null =
                    // a rule name was given but is unrecognized → fail-closed: a zeroed period, not "count everything".
                    var filtered = await _filterRuleResolver.ApplyStatisticsFilterAsync(statisticsContext.StoreId, bucket.Filter, criteria);

                    var period = filtered == null
                        ? StatisticsFieldHelper.EmptyPeriod<CustomerOrderStatisticsPeriod>(p => p.CurrencyCode = statisticsContext.CurrencyCode)
                        : await _statisticsService.GetStatisticsAsync(filtered);
                    return (bucket, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.bucket, x => x.period);
            });
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
