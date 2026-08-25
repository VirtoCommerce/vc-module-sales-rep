using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepCustomerInsightsService : ISalesRepCustomerInsightsService
{
    // Bounded reads: 'date' mode pulls the newest hour buckets and aggregates them here (older activity is
    // truncated); 'count' mode pulls GA-aggregated rows ordered by count, wide enough to survive dropped
    // "(not set)" rows and per-name splits of one product code.
    private const int DateModeBucketFetchSize = 200;
    private const int CountModeRowFetchSize = 50;

    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;

    public SalesRepCustomerInsightsService(IOptionalDependency<IAnalyticsService> analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public virtual async Task<bool> IsAvailableAsync(string storeId)
    {
        return _analyticsService.HasValue && await _analyticsService.Value.IsConfiguredAsync(storeId);
    }

    public virtual async Task<IList<SalesRepSearchTerm>> GetSearchTermsAsync(SalesRepCustomerInsightsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        // Only the 'search' event: the storefront fires both 'search' and 'view_search_results' for a single
        // search action, so counting both would double-count it.
        var events = await SearchEventsAsync(
            criteria,
            [AnalyticsConstants.EventNames.Search],
            [AnalyticsConstants.Dimensions.SearchTerm]);

        var terms = events
            .Select(x => (Term: GetDimension(x, AnalyticsConstants.Dimensions.SearchTerm), x.Count, Date: x.OccurredAt))
            .Where(x => !string.IsNullOrEmpty(x.Term))
            .GroupBy(x => x.Term, StringComparer.Ordinal)
            .Select(group =>
            {
                var term = AbstractTypeFactory<SalesRepSearchTerm>.TryCreateInstance();
                term.Term = group.Key;
                term.Count = group.Sum(x => x.Count);
                term.LastSearchedDate = group.Max(x => x.Date);
                return term;
            });

        terms = IsDateSort(criteria)
            ? terms.OrderByDescending(x => x.LastSearchedDate)
            : terms.OrderByDescending(x => x.Count).ThenBy(x => x.Term, StringComparer.Ordinal);

        return terms.Take(Math.Max(criteria.Take, 0)).ToList();
    }

    public virtual async Task<IList<SalesRepBrowsedProduct>> GetBrowsedProductsAsync(SalesRepCustomerInsightsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var events = await SearchEventsAsync(
            criteria,
            [AnalyticsConstants.EventNames.ViewItem],
            [AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName]);

        var products = events
            .Select(x => (
                Code: GetDimension(x, AnalyticsConstants.Dimensions.ItemId),
                Name: GetDimension(x, AnalyticsConstants.Dimensions.ItemName),
                x.Count,
                Date: x.OccurredAt))
            .Where(x => !string.IsNullOrEmpty(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var product = AbstractTypeFactory<SalesRepBrowsedProduct>.TryCreateInstance();
                product.Code = group.Key;
                product.Name = group
                    .OrderByDescending(x => x.Date)
                    .Select(x => x.Name)
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x));
                product.ViewCount = group.Sum(x => x.Count);
                product.LastViewedDate = group.Max(x => x.Date);
                return product;
            });

        products = IsDateSort(criteria)
            ? products.OrderByDescending(x => x.LastViewedDate)
            : products.OrderByDescending(x => x.ViewCount).ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase);

        return products.Take(Math.Max(criteria.Take, 0)).ToList();
    }

    protected virtual async Task<IList<AnalyticsEvent>> SearchEventsAsync(
        SalesRepCustomerInsightsCriteria criteria,
        string[] eventNames,
        string[] dimensionNames)
    {
        if (!_analyticsService.HasValue || string.IsNullOrEmpty(criteria.OrganizationId) || criteria.Take <= 0)
        {
            return [];
        }

        var isDateSort = IsDateSort(criteria);

        var searchCriteria = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();
        searchCriteria.StoreId = criteria.StoreId;
        searchCriteria.EventNames = eventNames;
        searchCriteria.DimensionNames = dimensionNames;
        searchCriteria.DimensionFilters = SalesRepAnalyticsScope.CreateScopeFilters([criteria.OrganizationId]);
        searchCriteria.From = criteria.From;
        searchCriteria.To = criteria.To;
        searchCriteria.SortBy = isDateSort ? AnalyticsConstants.SortBy.Date : AnalyticsConstants.SortBy.Count;
        searchCriteria.Take = isDateSort ? DateModeBucketFetchSize : CountModeRowFetchSize;

        return (await _analyticsService.Value.SearchEventsAsync(searchCriteria)).Events;
    }

    protected static bool IsDateSort(SalesRepCustomerInsightsCriteria criteria)
    {
        return ModuleConstants.Insights.Sort.Date.EqualsIgnoreCase(criteria.SortBy);
    }

    protected static string GetDimension(AnalyticsEvent analyticsEvent, string dimensionName)
    {
        return analyticsEvent.Dimensions?.TryGetValue(dimensionName, out var value) == true ? value : null;
    }
}
