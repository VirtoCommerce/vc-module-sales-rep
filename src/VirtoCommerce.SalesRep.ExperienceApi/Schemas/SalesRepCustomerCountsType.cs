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
/// The Sales Rep "my customers" counters (dashboard "My Customers" widget). <c>assignedCustomers</c> is the size of
/// the rep's served-organization set; <c>period(from, to)</c> and <c>comparison(current, previous)</c> expose the
/// range-dependent counters (customers who ordered, customers new in the range), coalesced per range by a DataLoader.
/// </summary>
public class SalesRepCustomerCountsType : ExtendableGraphType<SalesRepCustomerCountsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ISalesRepCustomerCountsService _countsService;
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;

    public SalesRepCustomerCountsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ISalesRepCustomerCountsService countsService,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _countsService = countsService;
        _filterRuleResolver = filterRuleResolver;

        Name = "SalesRepCustomerCounts";

        Field<NonNullGraphType<IntGraphType>>("assignedCustomers")
            .Description("Number of customers (organizations) the rep is assigned to serve.")
            .Resolve(context => context.Source.OrganizationIds?.Count ?? 0);

        Field<SalesRepCustomerCountsPeriodType>("period")
            .Description("Customer counters for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.FromArgument, "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.ToArgument, "Inclusive upper bound on the order created date (null = no upper bound).")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional customer-segment rule name (a salesRepCustomerFilterRules 'name'); counts only customers matching that segment. Omit for all served customers.")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>(StatisticsFieldHelper.FromArgument);
                var to = context.GetArgument<DateTime?>(StatisticsFieldHelper.ToArgument);
                return GetPeriodLoader(context).LoadAsync((from, to, StatisticsFieldHelper.GetFilter(context)));
            });

        Field<SalesRepCustomerCountsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a bucket shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.CurrentArgument, "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.PreviousArgument, "The baseline period to compare against.")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional customer-segment rule name applied to both periods (see 'period.filter').")
            .Resolve(context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.CurrentArgument);
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.PreviousArgument);
                var filterKey = StatisticsFieldHelper.GetFilter(context);
                var loader = GetPeriodLoader(context);

                var currentResult = loader.LoadAsync((current.From, current.To, filterKey));
                var previousResult = loader.LoadAsync((previous.From, previous.To, filterKey));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    private IDataLoader<(DateTime? From, DateTime? To, string Filter), SalesRepCustomerCountsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var countsContext = (SalesRepCustomerCountsContext)context.Source;

        var loaderKey = $"{nameof(SalesRepCustomerCountsType)}:{countsContext.SalesRepUserId}:{string.Join(',', countsContext.OrganizationIds)}:{countsContext.StoreId}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Filter), SalesRepCustomerCountsPeriod>(
            loaderKey,
            async buckets =>
            {
                var tasks = buckets.Select(async bucket =>
                {
                    var criteria = AbstractTypeFactory<SalesRepCustomerCountsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = countsContext.OrganizationIds;
                    criteria.AssignmentDates = countsContext.AssignmentDates;
                    criteria.CustomerId = countsContext.SalesRepUserId;
                    criteria.StoreId = countsContext.StoreId;
                    criteria.FromDate = bucket.From;
                    criteria.ToDate = bucket.To;

                    // Apply the selected customer segment through the shared resolver (same rule the customers list
                    // uses). Null = a segment name was given but is unrecognized → fail-closed: zeroed counters.
                    var filtered = await _filterRuleResolver.ApplyCountsFilterAsync(countsContext.StoreId, bucket.Filter, criteria);

                    var period = filtered == null
                        ? StatisticsFieldHelper.EmptyPeriod<SalesRepCustomerCountsPeriod>()
                        : await _countsService.GetCountsAsync(filtered);
                    return (bucket, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.bucket, x => x.period);
            });
    }

    private static SalesRepCustomerCountsComparison BuildComparison(SalesRepCustomerCountsPeriod current, SalesRepCustomerCountsPeriod previous)
    {
        var result = AbstractTypeFactory<SalesRepCustomerCountsComparison>.TryCreateInstance();

        result.OrderingCustomersChange = current.OrderingCustomers - previous.OrderingCustomers;
        result.OrderingCustomersChangePercent = StatisticsFieldHelper.Percent(previous.OrderingCustomers, current.OrderingCustomers);
        result.NewCustomersChange = current.NewCustomers - previous.NewCustomers;
        result.NewCustomersChangePercent = StatisticsFieldHelper.Percent(previous.NewCustomers, current.NewCustomers);

        return result;
    }
}
