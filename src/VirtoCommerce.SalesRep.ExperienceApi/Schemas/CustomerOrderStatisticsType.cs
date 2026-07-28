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

                // Queue both loads before chaining so they land in the same batch (one dispatch); a range shared
                // with a 'period' selection is then aggregated only once.
                var currentResult = loader.LoadAsync((current.From, current.To, filterKey));
                var previousResult = loader.LoadAsync((previous.From, previous.To, filterKey));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    private IDataLoader<(DateTime? From, DateTime? To, string Filter), CustomerOrderStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerOrderStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerOrderStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        // Per-request batch loader shared by 'period' and 'comparison': keyed on the shared context, with the range
        // in the batch key, so each distinct range is aggregated only once per request (no N+1).
        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Filter), CustomerOrderStatisticsPeriod>(
            loaderKey,
            async buckets =>
            {
                var tasks = buckets.Select(async bucket =>
                {
                    var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.FromDate = bucket.From;
                    criteria.ToDate = bucket.To;

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
