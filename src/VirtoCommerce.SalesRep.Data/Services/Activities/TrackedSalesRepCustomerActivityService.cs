using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.AnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

/// <summary>
/// The customer summary from the platform's own analytics store for a store that chose it, from Google Analytics
/// otherwise.
/// </summary>
public class TrackedSalesRepCustomerActivityService : SalesRepCustomerActivityService
{
    private readonly ISalesRepPlatformAnalytics _platformAnalytics;

    public TrackedSalesRepCustomerActivityService(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ISalesRepAnalyticsAvailability availability,
        ISalesRepAnalyticsScope scope,
        ILogger<SalesRepCustomerActivityService> logger,
        ISalesRepPlatformAnalytics platformAnalytics)
        : base(analyticsService, availability, scope, logger)
    {
        _platformAnalytics = platformAnalytics;
    }

    public override async Task<SalesRepCustomerActivitySummary> GetSummaryAsync(SalesRepCustomerActivityCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var searchService = string.IsNullOrEmpty(criteria.OrganizationId)
            ? null
            : await _platformAnalytics.GetSearchServiceAsync(criteria.StoreId);

        if (searchService == null)
        {
            return await base.GetSummaryAsync(criteria);
        }

        var result = AbstractTypeFactory<SalesRepCustomerActivitySummary>.TryCreateInstance();
        result.IsAnalyticsAvailable = true;

        var visitsTask = GetLatestAsync(searchService, criteria, ModuleConstants.Analytics.PlatformEventNames.SessionStart, withSubjectOnly: false);
        var lastSearchTask = GetLatestAsync(searchService, criteria, ModuleConstants.Analytics.PlatformEventNames.Search, withSubjectOnly: true);
        var lastProductViewTask = GetLatestAsync(searchService, criteria, ModuleConstants.Analytics.PlatformEventNames.ViewItem, withSubjectOnly: true);

        await Task.WhenAll(visitsTask, lastSearchTask, lastProductViewTask);

        var visits = await visitsTask;
        result.VisitsCount = visits.TotalCount;
        result.LastWebLogin = visits.Results.FirstOrDefault()?.OccurredAt;

        var lastSearch = (await lastSearchTask).Results.FirstOrDefault();
        result.LastSearchTerm = lastSearch?.Subject;
        result.LastSearchedDate = lastSearch?.OccurredAt;

        var lastProductView = (await lastProductViewTask).Results.FirstOrDefault();
        result.LastViewedDate = lastProductView?.OccurredAt;
        if (lastProductView != null)
        {
            result.LastViewedProduct = AbstractTypeFactory<SalesRepActivityProduct>.TryCreateInstance();
            result.LastViewedProduct.Code = lastProductView.Subject;
        }

        return result;
    }

    // The newest matching event, with the count of all of them.
    protected virtual async Task<AnalyticsEventSearchResult> GetLatestAsync(
        IAnalyticsEventSearchService searchService,
        SalesRepCustomerActivityCriteria criteria,
        string eventName,
        bool withSubjectOnly)
    {
        var searchCriteria = await _platformAnalytics.CreateCriteriaAsync(criteria.StoreId, [criteria.OrganizationId], eventName, criteria.From, criteria.To);
        searchCriteria.WithSubjectOnly = withSubjectOnly;
        searchCriteria.Take = 1;

        return await searchService.SearchAsync(searchCriteria);
    }
}
