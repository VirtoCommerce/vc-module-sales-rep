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
/// A Sales Rep's cart/project statistics in one currency (dashboard "Active Projects" and related cart widgets).
/// Request any number of ranges via aliased <c>period(from, to, kinds)</c> selections and <c>comparison(current,
/// previous, kinds)</c> selections; a per-(range, kind-filter) DataLoader coalesces them so each distinct bucket is
/// aggregated only once. <c>kinds</c> are business names (e.g. "project") the server maps to underlying cart
/// type/status filters via <see cref="ISalesRepCartKindService"/> — the client never sees internal cart types.
/// </summary>
public class CustomerCartStatisticsType : ExtendableGraphType<CustomerCartStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerCartStatisticsService _statisticsService;
    private readonly ISalesRepCartKindService _kindService;

    public CustomerCartStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerCartStatisticsService statisticsService,
        ISalesRepCartKindService kindService)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;
        _kindService = kindService;

        Name = "CustomerCartStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency all figures below are converted to.");

        Field<CustomerCartStatisticsPeriodType>("period")
            .Description("Cart statistics for a single date range. Omit both bounds for lifetime.")
            .Argument<DateTimeGraphType>("from", "Inclusive lower bound on the cart created date (null = no lower bound).")
            .Argument<DateTimeGraphType>("to", "Exclusive upper bound on the cart created date (null = no upper bound).")
            .Argument<ListGraphType<StringGraphType>>("kinds", "Optional cart-kind names (salesRepCartKinds 'name's, e.g. \"project\"); counts only carts matching the type/status filter those kinds resolve to. Omit for every cart.")
            .ResolveAsync(async context =>
            {
                var from = context.GetArgument<DateTime?>("from");
                var to = context.GetArgument<DateTime?>("to");

                var (filter, blocked) = await ResolveKindFilterAsync(context);
                if (blocked)
                {
                    return EmptyPeriod(context);
                }

                return GetPeriodLoader(context).LoadAsync(BuildKey(from, to, filter));
            });

        Field<CustomerCartStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a range shared with a 'period' selection is not queried again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("current", "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>("previous", "The baseline period to compare against.")
            .Argument<ListGraphType<StringGraphType>>("kinds", "Optional cart-kind names applied to both periods (see 'period.kinds').")
            .ResolveAsync(async context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>("current");
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>("previous");

                var (filter, blocked) = await ResolveKindFilterAsync(context);
                if (blocked)
                {
                    return EmptyComparison(context);
                }

                var loader = GetPeriodLoader(context);

                // Queue both loads before chaining so they land in the same batch (one dispatch).
                var currentResult = loader.LoadAsync(BuildKey(current.From, current.To, filter));
                var previousResult = loader.LoadAsync(BuildKey(previous.From, previous.To, filter));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    /// <summary>
    /// Resolves the field's <c>kinds</c> argument (business kind names) to the underlying cart type/status filter,
    /// via the shared <see cref="ISalesRepCartKindService"/>. Returns <c>(null, false)</c> when no kind filter was
    /// requested. Fail-closed: when kinds were given but resolve to an empty filter (all unrecognized), returns
    /// <c>(null, true)</c> so the caller yields zeros rather than counting every cart.
    /// </summary>
    private async Task<(SalesRepCartFilter Filter, bool Blocked)> ResolveKindFilterAsync(IResolveFieldContext context)
    {
        var kindNames = context.GetArgument<string[]>("kinds");
        if (kindNames == null || kindNames.Length == 0)
        {
            return (null, false);
        }

        var statisticsContext = (CustomerCartStatisticsContext)context.Source;
        var filter = await _kindService.ResolveCartFilterAsync(statisticsContext.StoreId, kindNames);
        return filter.IsEmpty ? (null, true) : (filter, false);
    }

    private static (DateTime? From, DateTime? To, string Types, string Statuses) BuildKey(DateTime? from, DateTime? to, SalesRepCartFilter filter)
        => (from, to, StatisticsFieldHelper.EncodeSet(filter?.Types), StatisticsFieldHelper.EncodeSet(filter?.Statuses));

    // A per-request batch loader shared by 'period' and 'comparison'. Keyed on the shared context (rep, organizations,
    // store, currency) so every distinct (range, kind-filter) under one node is aggregated exactly once.
    private IDataLoader<(DateTime? From, DateTime? To, string Types, string Statuses), CustomerCartStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerCartStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerCartStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Types, string Statuses), CustomerCartStatisticsPeriod>(
            loaderKey,
            async keys =>
            {
                var tasks = keys.Select(async key =>
                {
                    var criteria = AbstractTypeFactory<CustomerCartStatisticsCriteria>.TryCreateInstance();
                    criteria.OrganizationIds = statisticsContext.OrganizationIds;
                    criteria.CustomerId = statisticsContext.SalesRepUserId;
                    criteria.StoreId = statisticsContext.StoreId;
                    criteria.CurrencyCode = statisticsContext.CurrencyCode;
                    criteria.Types = StatisticsFieldHelper.DecodeSet(key.Types);
                    criteria.Statuses = StatisticsFieldHelper.DecodeSet(key.Statuses);
                    criteria.FromDate = key.From;
                    criteria.ToDate = key.To;

                    var period = await _statisticsService.GetStatisticsAsync(criteria);
                    return (key, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.key, x => x.period);
            });
    }

    private static CustomerCartStatisticsPeriod EmptyPeriod(IResolveFieldContext context)
    {
        var period = AbstractTypeFactory<CustomerCartStatisticsPeriod>.TryCreateInstance();
        period.CurrencyCode = ((CustomerCartStatisticsContext)context.Source).CurrencyCode;
        return period;
    }

    // Fail-closed comparison: both sides zero (BuildComparison(empty, empty) → zero changes, null percents).
    private static CustomerCartStatisticsComparison EmptyComparison(IResolveFieldContext context)
    {
        var empty = EmptyPeriod(context);
        return BuildComparison(empty, empty);
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
