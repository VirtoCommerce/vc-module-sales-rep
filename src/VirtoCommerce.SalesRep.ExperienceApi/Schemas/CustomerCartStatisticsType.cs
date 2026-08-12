using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerCartStatisticsType : ExtendableGraphType<CustomerCartStatisticsContext>
{
    private readonly IDataLoaderContextAccessor _dataLoaderContextAccessor;
    private readonly ICustomerCartStatisticsService _statisticsService;
    private readonly ISalesRepCartFilterRuleResolver _filterRuleResolver;

    public CustomerCartStatisticsType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ICustomerCartStatisticsService statisticsService,
        ISalesRepCartFilterRuleResolver filterRuleResolver)
    {
        _dataLoaderContextAccessor = dataLoaderContextAccessor;
        _statisticsService = statisticsService;
        _filterRuleResolver = filterRuleResolver;

        Name = "CustomerCartStatistics";

        Field(x => x.CurrencyCode, nullable: false).Description("Currency the money figures below are converted to.");

        Field<CustomerCartStatisticsPeriodType>("period")
            .Description("Cart figures for a single date range. Omit both bounds for what is in the carts right now.")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.FromArgument, "Inclusive lower bound on each line item's modified date (null = no lower bound); the carts' own dates are never filtered.")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.ToArgument, "Inclusive upper bound on each line item's modified date (null = no upper bound); the carts' own dates are never filtered.")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional cart-kind rule name (a salesRepCartFilterRules 'name', e.g. \"active-carts\"); aggregates only the line items of carts matching that rule's name/type/status filter. Omit for every cart row, wishlists and other lists included.")
            .Resolve(context =>
            {
                var from = context.GetArgument<DateTime?>(StatisticsFieldHelper.FromArgument);
                var to = context.GetArgument<DateTime?>(StatisticsFieldHelper.ToArgument);
                var withCartFigures = RequestsCartFigures(context, CustomerCartStatisticsPeriodType.CartFigureFields);
                return GetPeriodLoader(context)
                    .LoadAsync((from, to, StatisticsFieldHelper.GetFilter(context), withCartFigures));
            });

        Field<CustomerCartStatisticsComparisonType>("comparison")
            .Description("Compares two periods (current vs previous). Reuses the period results, so a bucket a 'period' selection already asked for on the same terms is not aggregated again.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.CurrentArgument, "The later period.")
            .Argument<NonNullGraphType<SalesRepStatisticsPeriodInputType>>(StatisticsFieldHelper.PreviousArgument, "The baseline period to compare against.")
            .Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Optional cart-kind rule name applied to both periods (see 'period.filter').")
            .Resolve(context =>
            {
                var current = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.CurrentArgument);
                var previous = context.GetArgument<SalesRepStatisticsPeriodInput>(StatisticsFieldHelper.PreviousArgument);
                var filterKey = StatisticsFieldHelper.GetFilter(context);

                // A money delta needs the money on BOTH sides, so the flag goes to both loads.
                var withCartFigures = RequestsCartFigures(context, CustomerCartStatisticsComparisonType.CartFigureFields);

                var loader = GetPeriodLoader(context);

                // Queue both loads before chaining so they land in the same batch (one dispatch); a range another
                // selection already asked for with the same cart-figure flag is then aggregated only once.
                var currentResult = loader.LoadAsync((current.From, current.To, filterKey, withCartFigures));
                var previousResult = loader.LoadAsync((previous.From, previous.To, filterKey, withCartFigures));

                return currentResult.Then(currentPeriod =>
                    previousResult.Then(previousPeriod => BuildComparison(currentPeriod, previousPeriod)));
            });
    }

    /// <summary>
    /// Whether this selection asks for a cart-level figure, so the aggregation can skip the second scan and the
    /// currency conversion when only the item quantities are wanted. Reads the selected field NAMES, so aliases,
    /// fragments and @skip/@include are all honoured.
    /// </summary>
    private static bool RequestsCartFigures(IResolveFieldContext context, string[] cartFigureFields)
    {
        var selectedFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];

        return cartFigureFields.Any(selectedFields.IncludesField);
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
        result.SelectedItemQuantityChange = current.SelectedItemQuantity - previous.SelectedItemQuantity;
        result.SelectedItemQuantityChangePercent = StatisticsFieldHelper.Percent(previous.SelectedItemQuantity, current.SelectedItemQuantity);
        result.UnselectedItemQuantityChange = current.UnselectedItemQuantity - previous.UnselectedItemQuantity;
        result.UnselectedItemQuantityChangePercent = StatisticsFieldHelper.Percent(previous.UnselectedItemQuantity, current.UnselectedItemQuantity);

        return result;
    }

    private IDataLoader<(DateTime? From, DateTime? To, string Filter, bool WithCartFigures), CustomerCartStatisticsPeriod> GetPeriodLoader(IResolveFieldContext context)
    {
        var statisticsContext = (CustomerCartStatisticsContext)context.Source;

        var loaderKey = $"{nameof(CustomerCartStatisticsType)}:{statisticsContext.SalesRepUserId}:{string.Join(',', statisticsContext.OrganizationIds)}:{statisticsContext.StoreId}:{statisticsContext.CurrencyCode}";

        // Per-request batch loader: keyed on the shared context, with the range in the batch key, so each distinct
        // range is aggregated only once per request however many aliases select it (no N+1). The cart-figure flag is
        // part of that key too — otherwise two aliases over the same range but different selections would collide
        // and a quantities-only result could answer the one that asked for the money.
        return _dataLoaderContextAccessor.Context.GetOrAddBatchLoader<(DateTime? From, DateTime? To, string Filter, bool WithCartFigures), CustomerCartStatisticsPeriod>(
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
                    criteria.IncludeCartFigures = bucket.WithCartFigures;

                    var filtered = await _filterRuleResolver.ApplyStatisticsFilterAsync(statisticsContext.StoreId, bucket.Filter, criteria);

                    var period = filtered == null
                        ? StatisticsFieldHelper.EmptyPeriod<CustomerCartStatisticsPeriod>(p => p.CurrencyCode = statisticsContext.CurrencyCode)
                        : await _statisticsService.GetStatisticsAsync(filtered);
                    return (bucket, period);
                });

                var results = await Task.WhenAll(tasks);
                return results.ToDictionary(x => x.bucket, x => x.period);
            });
    }
}
