using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        new(
            ActivityConstants.Categories.Searches,
            ActivityConstants.Types.Search,
            [AnalyticsConstants.EventNames.Search, AnalyticsConstants.EventNames.ViewSearchResults],
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

    public AnalyticsSalesRepActivitySource(IOptionalDependency<IAnalyticsService> analyticsService)
    {
        _analyticsService = analyticsService;
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

        var searchResult = await analyticsService.SearchEventsAsync(CreateSearchCriteria(category, criteria));

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Events
            .Where(x => x.OccurredAt != null)
            .Select(x => ToEvent(category, x))
            .ToList();

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
