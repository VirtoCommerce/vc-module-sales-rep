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
/// A Sales Rep's cart/project statistics in one currency (dashboard "Active Projects" and related cart widgets).
/// Request any number of ranges via aliased <c>period(from, to, kinds)</c> selections and <c>comparison(current,
/// previous, kinds)</c> selections; a per-(range, kind-selection) DataLoader coalesces them so each distinct bucket
/// is aggregated only once. <c>kinds</c> are business names (e.g. the built-in "active-carts") the server maps to underlying cart
/// type/status filters via <see cref="ISalesRepCartFilterRuleResolver"/> — the client never sees internal cart types, and
/// the mapping (plus fail-closed handling) happens inside the loader, so this graph type sees no concrete filter.
/// </summary>
public class CustomerCartStatisticsType : ExtendableGraphType<CustomerCartStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerCartStatisticsService _statisticsService;
    private readonly ISalesRepCartFilterRuleResolver _kindService;

    public CustomerCartStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerCartStatisticsService statisticsService,
        ISalesRepCartFilterRuleResolver kindService)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;
        _kindService = kindService;

        Name = "CustomerCartStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency all figures below are converted to.");

        Field<CustomerCartStatisticsPeriodType>("period")
            .Description("Cart statistics for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>("from", "Inclusive lower bound on the cart created date (null = no lower bound).")
            .Argument<DateTimeGraphType>("to", "Inclusive upper bound on the cart created date (null = no upper bound).")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional cart-kind rule name (a salesRepCartFilterRules 'name', e.g. \"active-carts\"); counts only carts matching that rule's type/status/contents filter. Omit for every cart.")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>("from");
                var to = context.GetArgument<DateTime?>("to");
                return GetPeriodLoader(context).LoadAsync((from, to, StatisticsFieldHelper.GetFilter(context)));
            });

        Field<CustomerCartStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a bucket shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("current", "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("previous", "The baseline period to compare against.")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional cart-kind rule name applied to both periods (see 'period.filter').")
            .Resolve(context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>("current");
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>("previous");
                var filterKey = StatisticsFieldHelper.GetFilter(context);

                var loader = GetPeriodLoader(context);

                // Queue both loads before chaining so they land in the same batch (one dispatch).
                var currentResult = loader.LoadAsync((current.From, current.To, filterKey));
                var previousResult = loader.LoadAsync((previous.From, previous.To, filterKey));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    // A per-request batch loader shared by 'period' and 'comparison'. Keyed on the shared context (rep, organizations,
    // store, currency); the batch key adds the range and the raw kind selection. Kind resolution + fail-closed
    // handling happen here, once per distinct bucket.
    private IDataLoader<(DateTime? From, DateTime? To, string Filter), CustomerCartStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerCartStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerCartStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Filter), CustomerCartStatisticsPeriod>(
            loaderKey,
            async buckets =>
            {
                var tasks = buckets.Select(async bucket =>
                {
                    var criteria = AbstractTypeFactory<CustomerCartStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.FromDate = bucket.From;
                    criteria.ToDate = bucket.To;

                    // Apply the selected rule's type/status filter through the shared resolver. Null = a rule name
                    // was given but is unrecognized → fail-closed: a zeroed period, not "count every cart".
                    var filtered = await _kindService.ApplyStatisticsFilterAsync(statisticsContext.StoreId, bucket.Filter, criteria);

                    var period = filtered == null
                        ? EmptyPeriod(statisticsContext.CurrencyCode)
                        : await _statisticsService.GetStatisticsAsync(filtered);
                    return (bucket, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.bucket, x => x.period);
            });
    }

    private static CustomerCartStatisticsPeriod EmptyPeriod(string currencyCode)
    {
        var period = AbstractTypeFactory<CustomerCartStatisticsPeriod>.TryCreateInstance();
        period.CurrencyCode = currencyCode;
        return period;
    }

    private static CustomerCartStatisticsComparison BuildComparison(CustomerCartStatisticsPeriod current, CustomerCartStatisticsPeriod previous)
    {
        var result = AbstractTypeFactory<CustomerCartStatisticsComparison>.TryCreateInstance();

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
