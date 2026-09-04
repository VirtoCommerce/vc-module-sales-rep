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
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepCustomerActivityService : ISalesRepCustomerActivityService
{
    // Hour-bucket rows can miss a dimension (GA "(not set)"); look a few rows deep for the latest usable one.
    private const int LastEventLookupSize = 10;

    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;
    private readonly ISalesRepAnalyticsAvailability _availability;

    public SalesRepCustomerActivityService(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ISalesRepAnalyticsAvailability availability)
    {
        _analyticsService = analyticsService;
        _availability = availability;
    }

    public virtual async Task<SalesRepCustomerActivitySummary> GetSummaryAsync(SalesRepCustomerActivityCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepCustomerActivitySummary>.TryCreateInstance();

        if (!_analyticsService.HasValue || string.IsNullOrEmpty(criteria.OrganizationId))
        {
            return result;
        }

        var analyticsService = _analyticsService.Value;

        result.IsAnalyticsConfigured = await _availability.IsConfiguredAsync(criteria.StoreId);
        if (!result.IsAnalyticsConfigured)
        {
            return result;
        }

        // The three reads are independent and cached under distinct keys, so they do not serialize on each other.
        var loginSummaryTask = GetLoginSummaryAsync(analyticsService, criteria);
        var lastSearchTermTask = GetLastSearchTermAsync(analyticsService, criteria);
        var lastViewedProductTask = GetLastViewedProductAsync(analyticsService, criteria);

        await Task.WhenAll(loginSummaryTask, lastSearchTermTask, lastViewedProductTask);

        var loginSummary = await loginSummaryTask;
        result.VisitsCount = loginSummary?.TotalCount ?? 0;
        result.LastWebLogin = loginSummary?.LastOccurredAt;
        result.LastSearchTerm = await lastSearchTermTask;
        result.LastViewedProduct = await lastViewedProductTask;

        return result;
    }

    protected virtual async Task<AnalyticsEventSummary> GetLoginSummaryAsync(
        IAnalyticsService analyticsService,
        SalesRepCustomerActivityCriteria criteria)
    {
        var summaries = await analyticsService.GetEventSummariesAsync(CreateLoginSummaryCriteria(criteria));

        return summaries.FirstOrDefault(x => x.EventName.EqualsIgnoreCase(AnalyticsConstants.EventNames.Login));
    }

    protected virtual AnalyticsEventSummaryCriteria CreateLoginSummaryCriteria(SalesRepCustomerActivityCriteria criteria)
    {
        var result = AbstractTypeFactory<AnalyticsEventSummaryCriteria>.TryCreateInstance();

        result.StoreId = criteria.StoreId;
        result.EventNames = [AnalyticsConstants.EventNames.Login];
        result.DimensionFilters = SalesRepAnalyticsScope.CreateScopeFilters([criteria.OrganizationId]);
        result.From = criteria.From;
        result.To = criteria.To;

        return result;
    }

    protected virtual async Task<string> GetLastSearchTermAsync(
        IAnalyticsService analyticsService,
        SalesRepCustomerActivityCriteria criteria)
    {
        var searchCriteria = CreateEventSearchCriteria(
            criteria,
            [AnalyticsConstants.EventNames.Search, AnalyticsConstants.EventNames.ViewSearchResults],
            [AnalyticsConstants.Dimensions.SearchTerm]);

        var searchResult = await analyticsService.SearchEventsAsync(searchCriteria);

        return searchResult.Events
            .Select(x => SalesRepAnalyticsScope.GetDimension(x, AnalyticsConstants.Dimensions.SearchTerm))
            .FirstOrDefault(x => !string.IsNullOrEmpty(x));
    }

    protected virtual async Task<SalesRepActivityProduct> GetLastViewedProductAsync(
        IAnalyticsService analyticsService,
        SalesRepCustomerActivityCriteria criteria)
    {
        var searchCriteria = CreateEventSearchCriteria(
            criteria,
            [AnalyticsConstants.EventNames.ViewItem],
            [AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName]);

        var searchResult = await analyticsService.SearchEventsAsync(searchCriteria);

        var lastViewed = searchResult.Events
            .FirstOrDefault(x => !string.IsNullOrEmpty(SalesRepAnalyticsScope.GetDimension(x, AnalyticsConstants.Dimensions.ItemId)));
        if (lastViewed == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<SalesRepActivityProduct>.TryCreateInstance();
        result.Code = SalesRepAnalyticsScope.GetDimension(lastViewed, AnalyticsConstants.Dimensions.ItemId);
        result.Name = SalesRepAnalyticsScope.GetDimension(lastViewed, AnalyticsConstants.Dimensions.ItemName);

        return result;
    }

    protected virtual AnalyticsEventSearchCriteria CreateEventSearchCriteria(
        SalesRepCustomerActivityCriteria criteria,
        IList<string> eventNames,
        IList<string> dimensionNames)
    {
        var result = SalesRepAnalyticsScope.CreateCriteria(
            criteria.StoreId,
            [criteria.OrganizationId],
            eventNames,
            dimensionNames,
            criteria.From,
            criteria.To);

        result.Take = LastEventLookupSize;

        return result;
    }
}
