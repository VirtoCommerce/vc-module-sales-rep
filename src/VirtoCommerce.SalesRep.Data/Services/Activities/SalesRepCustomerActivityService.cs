using System;
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

    public SalesRepCustomerActivityService(IOptionalDependency<IAnalyticsService> analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public virtual async Task<SalesRepCustomerActivitySummary> GetSummaryAsync(SalesRepCustomerActivityCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepCustomerActivitySummary>.TryCreateInstance();

        if (!_analyticsService.HasValue || string.IsNullOrEmpty(criteria.OrganizationId))
        {
            return result;
        }

        result.IsAnalyticsConfigured = await _analyticsService.Value.IsConfiguredAsync(criteria.StoreId);
        if (!result.IsAnalyticsConfigured)
        {
            return result;
        }

        var loginSummary = (await _analyticsService.Value.GetEventSummariesAsync(CreateLoginSummaryCriteria(criteria)))
            .FirstOrDefault(x => x.EventName == AnalyticsConstants.EventNames.Login);
        result.VisitsCount = loginSummary?.TotalCount ?? 0;
        result.LastWebLogin = loginSummary?.LastOccurredAt;

        result.LastSearchTerm = await GetLastSearchTermAsync(criteria);
        result.LastViewedProduct = await GetLastViewedProductAsync(criteria);

        return result;
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

    protected virtual async Task<string> GetLastSearchTermAsync(SalesRepCustomerActivityCriteria criteria)
    {
        var searchCriteria = CreateEventSearchCriteria(
            criteria,
            [AnalyticsConstants.EventNames.Search, AnalyticsConstants.EventNames.ViewSearchResults],
            [AnalyticsConstants.Dimensions.SearchTerm]);

        var searchResult = await _analyticsService.Value.SearchEventsAsync(searchCriteria);

        return searchResult.Events
            .Select(x => GetDimension(x, AnalyticsConstants.Dimensions.SearchTerm))
            .FirstOrDefault(x => !string.IsNullOrEmpty(x));
    }

    protected virtual async Task<SalesRepActivityProduct> GetLastViewedProductAsync(SalesRepCustomerActivityCriteria criteria)
    {
        var searchCriteria = CreateEventSearchCriteria(
            criteria,
            [AnalyticsConstants.EventNames.ViewItem],
            [AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName]);

        var searchResult = await _analyticsService.Value.SearchEventsAsync(searchCriteria);

        var lastViewed = searchResult.Events
            .FirstOrDefault(x => !string.IsNullOrEmpty(GetDimension(x, AnalyticsConstants.Dimensions.ItemId)));
        if (lastViewed == null)
        {
            return null;
        }

        var result = AbstractTypeFactory<SalesRepActivityProduct>.TryCreateInstance();
        result.Code = GetDimension(lastViewed, AnalyticsConstants.Dimensions.ItemId);
        result.Name = GetDimension(lastViewed, AnalyticsConstants.Dimensions.ItemName);

        return result;
    }

    protected virtual AnalyticsEventSearchCriteria CreateEventSearchCriteria(
        SalesRepCustomerActivityCriteria criteria,
        string[] eventNames,
        string[] dimensionNames)
    {
        var result = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();

        result.StoreId = criteria.StoreId;
        result.EventNames = eventNames;
        result.DimensionNames = dimensionNames;
        result.DimensionFilters = SalesRepAnalyticsScope.CreateScopeFilters([criteria.OrganizationId]);
        result.From = criteria.From;
        result.To = criteria.To;
        result.Take = LastEventLookupSize;

        return result;
    }

    protected static string GetDimension(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.TryGetValue(dimensionName, out var value) == true ? value : null;
    }
}
