using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using Statuses = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStatuses;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepAnalyticsDiagnosticsService : ISalesRepAnalyticsDiagnosticsService
{
    private const string SearchTermsReportName = "searchTerms";
    private const string BrowsedProductsReportName = "browsedProducts";
    private const int FeatureQueryTake = 5;
    private const int FeatureQueryDays = 30;

    // Store-wide, unlike the widgets it mirrors: the audience filters but no organization filter, so it answers "does
    // the production read path return rows for this store at all".
    private static readonly string _probeScopeNote =
        $"store-wide probe: {DescribeFilters(SalesRepAnalyticsScope.CreateAudienceFilters())}, no organization filter, last {FeatureQueryDays} days, top {FeatureQueryTake}";

    private readonly IOptionalDependency<IAnalyticsDiagnosticsService> _diagnosticsService;
    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;

    public SalesRepAnalyticsDiagnosticsService(
        IOptionalDependency<IAnalyticsDiagnosticsService> diagnosticsService,
        IOptionalDependency<IAnalyticsService> analyticsService)
    {
        _diagnosticsService = diagnosticsService;
        _analyticsService = analyticsService;
    }

    public virtual async Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, bool includeLiveData)
    {
        var diagnosticsService = _diagnosticsService.Value;
        if (diagnosticsService == null)
        {
            var result = AbstractTypeFactory<AnalyticsDiagnosticsResult>.TryCreateInstance();
            result.Checks.Add(CreateCheck(AnalyticsConstants.DiagnosticsStages.Configuration, Statuses.Failed,
                "VirtoCommerce.GoogleEcommerceAnalytics module is not installed"));
            return result;
        }

        var diagnosticsResult = await diagnosticsService.RunAsync(storeId, CreateRequest(includeLiveData));
        diagnosticsResult.Checks.Add(await RunFeatureQueryAsync(storeId, includeLiveData));
        return diagnosticsResult;
    }

    protected virtual AnalyticsDiagnosticsRequest CreateRequest(bool includeLiveData)
    {
        var request = AbstractTypeFactory<AnalyticsDiagnosticsRequest>.TryCreateInstance();

        request.UserDimensionNames = [.. AnalyticsConstants.UserDimensions.AllNames];

        request.EventNames =
        [
            AnalyticsConstants.EventNames.Search,
            AnalyticsConstants.EventNames.ViewSearchResults,
            AnalyticsConstants.EventNames.ViewItem,
            AnalyticsConstants.EventNames.Login,
        ];

        request.Reports =
        [
            CreateReportShape(SearchTermsReportName,
                [AnalyticsConstants.Dimensions.EventName, AnalyticsConstants.Dimensions.DateHour, AnalyticsConstants.Dimensions.SearchTerm],
                AnalyticsConstants.Metrics.EventCount,
                [AnalyticsConstants.EventNames.Search, AnalyticsConstants.EventNames.ViewSearchResults]),
            CreateReportShape(BrowsedProductsReportName,
                [AnalyticsConstants.Dimensions.DateHour, AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName],
                AnalyticsConstants.Metrics.ItemsViewed,
                [AnalyticsConstants.EventNames.ViewItem]),
        ];

        request.IncludeLiveData = includeLiveData;

        return request;
    }

    protected virtual async Task<AnalyticsDiagnosticsCheck> RunFeatureQueryAsync(string storeId, bool includeLiveData)
    {
        if (!includeLiveData)
        {
            return CreateFeatureQueryCheck(Statuses.Skipped, "Skipped: live-data checks disabled by request.");
        }

        try
        {
            if (!await _analyticsService.Value.IsConfiguredAsync(storeId))
            {
                return CreateFeatureQueryCheck(Statuses.Warning,
                    $"Google Analytics is not configured for store '{storeId}' — feature queries were not executed.");
            }

            var rowCounts = await Task.WhenAll(
                CountFeatureRowsAsync(storeId, AnalyticsConstants.SortBy.Count,
                    [AnalyticsConstants.EventNames.Search],
                    [AnalyticsConstants.Dimensions.SearchTerm]),
                CountFeatureRowsAsync(storeId, AnalyticsConstants.SortBy.Date,
                    [AnalyticsConstants.EventNames.ViewItem],
                    [AnalyticsConstants.Dimensions.ItemId, AnalyticsConstants.Dimensions.ItemName]));
            var searchTermRows = rowCounts[0];
            var productViewRows = rowCounts[1];

            var counts = $"{SearchTermsReportName}={searchTermRows} rows, {BrowsedProductsReportName}={productViewRows} rows ({_probeScopeNote})";

            return searchTermRows == 0 && productViewRows == 0
                ? CreateFeatureQueryCheck(Statuses.Warning,
                    $"No processed rows yet — expected within 24–48 hours of first tagged traffic. {counts}.")
                : CreateFeatureQueryCheck(Statuses.Passed,
                    $"Feature queries succeeded through the production path: {counts}.");
        }
        catch (Exception ex)
        {
            return CreateFeatureQueryCheck(Statuses.Failed, "Feature query failed.", ex.Message);
        }
    }

    protected virtual async Task<int> CountFeatureRowsAsync(string storeId, string sortBy, IList<string> eventNames, IList<string> dimensionNames)
    {
        var criteria = AbstractTypeFactory<AnalyticsEventSearchCriteria>.TryCreateInstance();
        criteria.StoreId = storeId;
        criteria.EventNames = eventNames;
        criteria.DimensionNames = dimensionNames;
        criteria.DimensionFilters = SalesRepAnalyticsScope.CreateAudienceFilters();
        criteria.From = DateTime.UtcNow.AddDays(-FeatureQueryDays);
        // Reports what Google answers now, not the cached answer the widgets get.
        criteria.BypassCache = true;
        criteria.SortBy = sortBy;
        criteria.Take = FeatureQueryTake;

        var result = await _analyticsService.Value.SearchEventsAsync(criteria);
        return result.Events?.Count ?? 0;
    }

    protected static AnalyticsDiagnosticsReportShape CreateReportShape(string name, IList<string> dimensionNames, string metricName, IList<string> eventNames)
    {
        var shape = AbstractTypeFactory<AnalyticsDiagnosticsReportShape>.TryCreateInstance();
        shape.Name = name;
        shape.DimensionNames = dimensionNames;
        shape.MetricName = metricName;
        shape.EventNames = eventNames;
        return shape;
    }

    protected static AnalyticsDiagnosticsCheck CreateFeatureQueryCheck(string status, string message, string detail = null)
    {
        return CreateCheck(ModuleConstants.DiagnosticsStages.FeatureQuery, status, message, detail);
    }

    protected static AnalyticsDiagnosticsCheck CreateCheck(string stage, string status, string message, string detail = null)
    {
        var check = AbstractTypeFactory<AnalyticsDiagnosticsCheck>.TryCreateInstance();
        check.Stage = stage;
        check.Status = status;
        check.Message = message;
        check.Detail = detail;
        return check;
    }

    private static string DescribeFilters(IList<AnalyticsDimensionFilter> filters)
    {
        return string.Join(", ", filters.Select(x => $"{x.DimensionName}={string.Join("|", x.Values)}"));
    }
}
