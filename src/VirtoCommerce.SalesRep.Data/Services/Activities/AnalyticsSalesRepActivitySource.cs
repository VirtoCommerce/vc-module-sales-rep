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

    public virtual async Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        var effectiveCategories = criteria.GetEffectiveCategories(Categories);
        if (!_analyticsService.HasValue || effectiveCategories.Count == 0 || criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        List<SalesRepActivityEvent> merged = [];

        foreach (var category in _analyticsCategories.Where(x => effectiveCategories.Contains(x.Category)))
        {
            var searchResult = await _analyticsService.Value.SearchEventsAsync(CreateSearchCriteria(category, criteria));

            result.TotalCount += searchResult.TotalCount;
            merged.AddRange(searchResult.Events
                .Where(x => x.OccurredAt != null)
                .Select(x => ToEvent(category, x)));
        }

        result.Results = merged
            .OrderByDescending(x => x.OccurredAt)
            .Skip(criteria.Skip)
            .Take(Math.Max(criteria.Take, 0))
            .ToList();

        return result;
    }

    protected virtual AnalyticsEventSearchCriteria CreateSearchCriteria(SalesRepAnalyticsCategory category, SalesRepActivitySearchCriteria criteria)
    {
        var result = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();

        result.StoreId = criteria.StoreId;
        result.EventNames = category.EventNames.ToList();
        result.DimensionNames = category.DimensionNames.Append(AnalyticsConstants.UserDimensions.OrganizationId).ToList();
        result.DimensionFilters = SalesRepAnalyticsScope.CreateScopeFilters(criteria.OrganizationIds);
        result.From = criteria.From;
        result.To = criteria.To;
        // Each category fetches its own top rows; the caller slices the merge (per-source top skip+take pagination).
        result.Take = criteria.Skip + criteria.Take;
        result.Skip = 0;

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
        result.OrganizationId = GetDimension(analyticsEvent, AnalyticsConstants.UserDimensions.OrganizationId);
        result.SearchTerm = GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.SearchTerm);
        result.ProductCode = GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.ItemId);
        result.ProductName = GetDimension(analyticsEvent, AnalyticsConstants.Dimensions.ItemName);

        return result;
    }

    protected static string GetDimension(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.TryGetValue(dimensionName, out var value) == true ? value : null;
    }
}
