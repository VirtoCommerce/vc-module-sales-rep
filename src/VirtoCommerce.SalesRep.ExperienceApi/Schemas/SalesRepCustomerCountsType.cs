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
/// The Sales Rep "my customers" counters (dashboard "My Customers" widget). <c>assignedCustomers</c> is the size of
/// the rep's served-organization set; <c>period(from, to)</c> and <c>comparison(current, previous)</c> expose the
/// range-dependent counters (customers who ordered, customers new in the range), coalesced per range by a DataLoader.
/// </summary>
public class SalesRepCustomerCountsType : ExtendableGraphType<SalesRepCustomerCountsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ISalesRepCustomerCountsService _countsService;

    public SalesRepCustomerCountsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ISalesRepCustomerCountsService countsService)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _countsService = countsService;

        Name = "SalesRepCustomerCounts";

        Field<NonNullGraphType<IntGraphType>>("assignedCustomers")
            .Description("Number of customers (organizations) the rep is assigned to serve.")
            .Resolve(context => ((SalesRepCustomerCountsContext)context.Source).OrganizationIds?.Length ?? 0);

        Field<SalesRepCustomerCountsPeriodType>("period")
            .Description("Customer counters for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>("from", "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>("to", "Exclusive upper bound on the order created date (null = no upper bound).")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>("from");
                var to = context.GetArgument<DateTime?>("to");
                return GetPeriodLoader(context).LoadAsync((from, to));
            });

        Field<SalesRepCustomerCountsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a range shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("current", "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("previous", "The baseline period to compare against.")
            .Resolve(context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>("current");
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>("previous");
                var loader = GetPeriodLoader(context);

                var currentResult = loader.LoadAsync((current.From, current.To));
                var previousResult = loader.LoadAsync((previous.From, previous.To));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    private IDataLoader<(DateTime? From, DateTime? To), SalesRepCustomerCountsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var countsContext = (SalesRepCustomerCountsContext)context.Source;

        var loaderKey = $"{nameof(SalesRepCustomerCountsType)}:{countsContext.SalesRepUserId}:{string.Join(',', countsContext.OrganizationIds)}:{countsContext.StoreId}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To), SalesRepCustomerCountsPeriod>(
            loaderKey,
            async ranges =>
            {
                var tasks = ranges.Select(async range =>
                {
                    var criteria = AbstractTypeFactory<SalesRepCustomerCountsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = countsContext.OrganizationIds;
                    criteria.CustomerId = countsContext.SalesRepUserId;
                    criteria.StoreId = countsContext.StoreId;
                    criteria.FromDate = range.From;
                    criteria.ToDate = range.To;

                    var period = await _countsService.GetCountsAsync(criteria);
                    return (range, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.range, x => x.period);
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
