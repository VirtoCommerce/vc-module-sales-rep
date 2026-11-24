using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.AnalyticsModule.Core.Services;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

/// <summary>
/// Search terms and browsed products from the platform's own analytics store for a store that chose it, from Google
/// Analytics otherwise.
/// </summary>
public class TrackedSalesRepCustomerInsightsService : SalesRepCustomerInsightsService
{
    private readonly ISalesRepPlatformAnalytics _platformAnalytics;

    public TrackedSalesRepCustomerInsightsService(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ISalesRepAnalyticsAvailability availability,
        ISalesRepAnalyticsScope scope,
        ISalesRepPlatformAnalytics platformAnalytics)
        : base(analyticsService, availability, scope)
    {
        _platformAnalytics = platformAnalytics;
    }

    public override async Task<IList<SalesRepSearchTerm>> GetSearchTermsAsync(SalesRepCustomerInsightsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var summaries = await GetSummariesAsync(criteria, ModuleConstants.Analytics.PlatformEventNames.Search);
        if (summaries == null)
        {
            return await base.GetSearchTermsAsync(criteria);
        }

        return summaries
            .Select(x =>
            {
                var term = AbstractTypeFactory<SalesRepSearchTerm>.TryCreateInstance();
                term.Term = x.Subject;
                term.Count = x.Count;
                term.LastSearchedDate = x.LastOccurredAt;
                return term;
            })
            .ToList();
    }

    public override async Task<IList<SalesRepBrowsedProduct>> GetBrowsedProductsAsync(SalesRepCustomerInsightsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var summaries = await GetSummariesAsync(criteria, ModuleConstants.Analytics.PlatformEventNames.ViewItem);
        if (summaries == null)
        {
            return await base.GetBrowsedProductsAsync(criteria);
        }

        // The name is left to the catalog: the store keeps the product code, and the caller resolves it anyway.
        return summaries
            .Select(x =>
            {
                var product = AbstractTypeFactory<SalesRepBrowsedProduct>.TryCreateInstance();
                product.Code = x.Subject;
                product.ViewCount = x.Count;
                product.LastViewedDate = x.LastOccurredAt;
                return product;
            })
            .ToList();
    }

    // Null when the store reads Google Analytics.
    protected virtual async Task<IList<AnalyticsSubjectSummary>> GetSummariesAsync(SalesRepCustomerInsightsCriteria criteria, string eventName)
    {
        var searchService = await _platformAnalytics.GetSearchServiceAsync(criteria.StoreId);
        if (searchService == null)
        {
            return null;
        }

        if (criteria.OrganizationIds.IsNullOrEmpty() || criteria.Take <= 0)
        {
            return [];
        }

        try
        {
            var searchCriteria = await _platformAnalytics.CreateCriteriaAsync(criteria.StoreId, criteria.OrganizationIds, eventName, criteria.From, criteria.To);

            return await searchService.GetSubjectSummariesAsync(
                searchCriteria,
                IsDateSort(criteria) ? AnalyticsSummarySort.Date : AnalyticsSummarySort.Count,
                criteria.Take);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not AnalyticsException)
        {
            // The one failure the callers of these reads know: they answer "unavailable" instead of failing the query.
            throw new AnalyticsException("The platform analytics store could not be read.", ex);
        }
    }
}
