using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Extensions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using ActivityConstants = VirtoCommerce.SalesRep.Core.ModuleConstants.Activities;
using PlatformEventNames = VirtoCommerce.SalesRep.Core.ModuleConstants.Analytics.PlatformEventNames;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

/// <summary>
/// The feed's tracked categories from the platform's own analytics store for a store that chose it, from Google
/// Analytics otherwise. One source for both, so a category is never claimed twice and summed.
/// </summary>
public class TrackedSalesRepActivitySource : AnalyticsSalesRepActivitySource
{
    private sealed record PlatformCategory(string Category, string Type, string EventName, bool WithSubjectOnly);

    private static readonly PlatformCategory[] _platformCategories =
    [
        new(ActivityConstants.Categories.Searches, ActivityConstants.Types.Search, PlatformEventNames.Search, WithSubjectOnly: true),
        new(ActivityConstants.Categories.ProductViews, ActivityConstants.Types.ProductView, PlatformEventNames.ViewItem, WithSubjectOnly: true),
        new(ActivityConstants.Categories.Logins, ActivityConstants.Types.Login, PlatformEventNames.SessionStart, WithSubjectOnly: false),
    ];

    private readonly ISalesRepPlatformAnalytics _platformAnalytics;

    public TrackedSalesRepActivitySource(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ISalesRepAnalyticsScope scope,
        ILogger<AnalyticsSalesRepActivitySource> logger,
        ISalesRepPlatformAnalytics platformAnalytics)
        : base(analyticsService, scope, logger)
    {
        _platformAnalytics = platformAnalytics;
    }

    public override async Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var searchService = await _platformAnalytics.GetSearchServiceAsync(criteria.StoreId);
        if (searchService == null)
        {
            return await base.SearchAsync(criteria);
        }

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        var category = Array.Find(_platformCategories, x => criteria.IsCategoryRequested(x.Category));
        if (category == null || criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        var searchCriteria = await _platformAnalytics.CreateCriteriaAsync(criteria.StoreId, criteria.OrganizationIds, category.EventName, criteria.From, criteria.To);
        searchCriteria.WithSubjectOnly = category.WithSubjectOnly;
        searchCriteria.Skip = criteria.Skip;
        searchCriteria.Take = criteria.Take;

        var searchResult = await searchService.SearchAsync(searchCriteria);

        result.TotalCount = searchResult.TotalCount;
        result.EventCount = searchResult.TotalCount;
        result.Results = searchResult.Results.Select(x => ToEvent(category, x)).ToList();

        return result;
    }

    private static SalesRepActivityEvent ToEvent(PlatformCategory category, AnalyticsEvent analyticsEvent)
    {
        var result = AbstractTypeFactory<SalesRepActivityEvent>.TryCreateInstance();

        result.Category = category.Category;
        result.Type = category.Type;
        result.OccurredAt = analyticsEvent.OccurredAt;
        result.Precision = ActivityConstants.Precision.Exact;
        result.OrganizationId = analyticsEvent.OrganizationId;

        if (category.EventName == PlatformEventNames.Search)
        {
            result.SearchTerm = analyticsEvent.Subject;
        }
        else if (category.EventName == PlatformEventNames.ViewItem)
        {
            result.ProductCode = analyticsEvent.Subject;
            result.ProductName = GetItemName(analyticsEvent.Properties);
        }

        return result;
    }

    // The tracked name of the only item, shown when the catalog no longer has the product.
    private static string GetItemName(IDictionary<string, object> properties)
    {
        return properties != null
            && properties.TryGetValue("items", out var items)
            && items is JArray { Count: > 0 } array
            && array[0] is JObject item
                ? item.Value<string>("item_name")
                : null;
    }
}
