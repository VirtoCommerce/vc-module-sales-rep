using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using ActivityConstants = VirtoCommerce.SalesRep.Core.ModuleConstants.Activities;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class AnalyticsSalesRepActivitySource : ISalesRepActivitySource
{
    private static readonly SalesRepAnalyticsCategory[] _analyticsCategories =
    [
        // Only 'search': the storefront also fires 'view_search_results' for the same search journey, and GA
        // returns a row per event name, so asking for both would count one search twice. The cost is a search
        // that never went through the header dropdown. "Last search term" reads both — finding is not counting.
        new(
            ActivityConstants.Categories.Searches,
            ActivityConstants.Types.Search,
            [AnalyticsConstants.EventNames.Search],
            [AnalyticsConstants.Dimensions.SearchTerm]),
        new(
            ActivityConstants.Categories.ProductViews,
            ActivityConstants.Types.ProductView,
            [AnalyticsConstants.EventNames.ViewItem],
            [AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName]),
        new(
            ActivityConstants.Categories.Logins,
            ActivityConstants.Types.Login,
            [AnalyticsConstants.EventNames.Login],
            []),
    ];

    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;
    private readonly ILogger<AnalyticsSalesRepActivitySource> _logger;

    public AnalyticsSalesRepActivitySource(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ILogger<AnalyticsSalesRepActivitySource> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public IList<string> Categories { get; } = _analyticsCategories.Select(x => x.Category).ToList();

    // The aggregator drives one category per call and owns the merge/sort/slice across categories.
    public virtual async Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        var category = Array.Find(_analyticsCategories, x => criteria.IsCategoryRequested(x.Category));
        if (!_analyticsService.HasValue || category == null || criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        var analyticsService = _analyticsService.Value;

        AnalyticsEventSearchResult searchResult;

        try
        {
            searchResult = await analyticsService.SearchEventsAsync(CreateSearchCriteria(category, criteria));
        }
        catch (AnalyticsException ex)
        {
            // This category is merged with orders and customers, so letting it out would empty every tab over a
            // reporting problem. The rep keeps the rest of the feed; the cause is in the log.
            _logger.LogWarning(ex, "Analytics activity category {Category} is unavailable for store {StoreId}",
                category.Category, criteria.StoreId);

            return result;
        }

        // One row per (hour bucket x dimension tuple), not per tracked event. A row with no usable hour bucket
        // cannot be placed on a time-ordered feed, so it leaves the page.
        var rows = searchResult.Events.Where(x => x.OccurredAt != null).ToList();

        // Uncorrected on purpose: TotalCount describes the whole set while the drop is only visible on the
        // fetched page, so subtracting it made a badge change value when its own tab was selected.
        result.TotalCount = searchResult.TotalCount;
        result.Results = rows.Select(x => ToEvent(category, x)).ToList();

        return result;
    }

    protected virtual AnalyticsEventSearchCriteria CreateSearchCriteria(SalesRepAnalyticsCategory category, SalesRepActivitySearchCriteria criteria)
    {
        var result = SalesRepAnalyticsScope.CreateCriteria(
            criteria.StoreId,
            criteria.OrganizationIds,
            [.. category.EventNames],
            [.. category.DimensionNames, AnalyticsConstants.UserDimensions.OrganizationId],
            criteria.From,
            criteria.To);

        result.Take = criteria.Take;
        result.Skip = criteria.Skip;

        return result;
    }

    protected virtual SalesRepActivityEvent ToEvent(SalesRepAnalyticsCategory category, AnalyticsEvent analyticsEvent)
    {
        var result = AbstractTypeFactory<SalesRepActivityEvent>.TryCreateInstance();

        result.Category = category.Category;
        result.Type = category.Type;
        result.OccurredAt = analyticsEvent.OccurredAt.Value;
        result.Precision = ActivityConstants.Precision.Hour;
        result.Count = analyticsEvent.Count;
        result.OrganizationId = SalesRepAnalyticsScope.GetDimension(analyticsEvent, AnalyticsConstants.UserDimensions.OrganizationId);
        result.SearchTerm = SalesRepAnalyticsScope.GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.SearchTerm);
        result.ProductCode = SalesRepAnalyticsScope.GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.ItemId);
        result.ProductName = SalesRepAnalyticsScope.GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.ItemName);

        return result;
    }
}
