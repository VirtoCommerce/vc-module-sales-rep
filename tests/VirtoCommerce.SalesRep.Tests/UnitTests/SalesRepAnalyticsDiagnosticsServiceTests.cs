using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using Stages = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStages;
using Statuses = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants.DiagnosticsStatuses;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The diagnostics wrapper contract of <see cref="SalesRepAnalyticsDiagnosticsService"/>: module-absent short-circuit,
/// the sales-rep expectations forwarded to the analytics module's diagnostics, and the appended stage-7 feature-query
/// probe (real <see cref="IAnalyticsService"/> path, store-wide, session_kind=self only).
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepAnalyticsDiagnosticsServiceTests
{
    private const string StoreId = "B2B-store";

    [Fact]
    public async Task Run_ModuleAbsent_ReturnsSingleFailedConfigurationCheck()
    {
        var service = new SalesRepAnalyticsDiagnosticsService(
            Optional<IAnalyticsDiagnosticsService>(null),
            Optional<IAnalyticsService>(null));

        var result = await service.RunAsync(StoreId, includeLiveData: true);

        var check = result.Checks.Should().ContainSingle().Subject;
        check.Stage.Should().Be(Stages.Configuration);
        check.Status.Should().Be(Statuses.Failed);
        check.Message.Should().Be("VirtoCommerce.GoogleEcommerceAnalytics module is not installed");
    }

    [Fact]
    public async Task Run_ForwardsSalesRepExpectations()
    {
        var diagnostics = new FakeAnalyticsDiagnosticsService();
        var service = CreateService(diagnostics, new FakeAnalyticsService());

        await service.RunAsync(StoreId, includeLiveData: true);

        diagnostics.ReceivedStoreId.Should().Be(StoreId);

        var request = diagnostics.ReceivedRequest;
        request.UserDimensionNames.Should().Equal("contact_id", "organization_id", "organization_name", "is_sales_rep", "session_kind");
        request.EventNames.Should().Equal("search", "view_search_results", "view_item", "login");
        request.Reports.Should().HaveCount(2);

        var searchTerms = request.Reports[0];
        searchTerms.Name.Should().Be("searchTerms");
        searchTerms.DimensionNames.Should().Equal("eventName", "dateHour", "searchTerm");
        searchTerms.MetricName.Should().Be("eventCount");
        searchTerms.EventNames.Should().Equal("search", "view_search_results");

        var browsedProducts = request.Reports[1];
        browsedProducts.Name.Should().Be("browsedProducts");
        browsedProducts.DimensionNames.Should().Equal("dateHour", "itemId", "itemName");
        browsedProducts.MetricName.Should().Be("itemsViewed");
        browsedProducts.EventNames.Should().Equal("view_item");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_PassesIncludeLiveDataThrough(bool includeLiveData)
    {
        var diagnostics = new FakeAnalyticsDiagnosticsService();
        var analytics = new FakeAnalyticsService();
        var service = CreateService(diagnostics, analytics);

        var result = await service.RunAsync(StoreId, includeLiveData);

        diagnostics.ReceivedRequest.IncludeLiveData.Should().Be(includeLiveData);

        var featureQueryCheck = result.Checks[^1];
        featureQueryCheck.Stage.Should().Be(ModuleConstants.DiagnosticsStages.FeatureQuery);
        if (!includeLiveData)
        {
            featureQueryCheck.Status.Should().Be(Statuses.Skipped);
            analytics.ReceivedSearchCriteria.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Run_FeatureQuery_ReportsPerReportRowCounts()
    {
        var analytics = new FakeAnalyticsService();
        var occurredAt = DateTime.UtcNow.AddDays(-1);
        analytics.AddEvent("search", occurredAt, count: 3, organizationId: "org-1", dimensions: [("searchTerm", "pumps")]);
        analytics.AddEvent("view_item", occurredAt, count: 2, organizationId: "org-1", dimensions: [("itemId", "SKU-1"), ("itemName", "Pump")]);
        analytics.AddEvent("view_item", occurredAt, count: 1, organizationId: "org-2", dimensions: [("itemId", "SKU-2"), ("itemName", "Valve")]);
        analytics.AddEvent("view_item", occurredAt, count: 1, organizationId: "org-1",
            sessionKind: AnalyticsConstants.SessionKinds.Impersonated, dimensions: [("itemId", "SKU-3"), ("itemName", "Hose")]);

        var result = await CreateService(new FakeAnalyticsDiagnosticsService(), analytics).RunAsync(StoreId, includeLiveData: true);

        result.Checks.Should().HaveCount(8);

        var check = result.Checks[^1];
        check.Stage.Should().Be(ModuleConstants.DiagnosticsStages.FeatureQuery);
        check.Status.Should().Be(Statuses.Passed);
        check.Message.Should().Contain("searchTerms=1 rows").And.Contain("browsedProducts=2 rows").And.Contain("no organization filter");

        analytics.ReceivedSearchCriteria.Should().HaveCount(2);

        var searchTermsCriteria = analytics.ReceivedSearchCriteria[0];
        searchTermsCriteria.StoreId.Should().Be(StoreId);
        searchTermsCriteria.SortBy.Should().Be(AnalyticsConstants.SortBy.Count);
        searchTermsCriteria.EventNames.Should().Equal("search");
        searchTermsCriteria.DimensionNames.Should().Equal("searchTerm");

        var productViewsCriteria = analytics.ReceivedSearchCriteria[1];
        productViewsCriteria.SortBy.Should().Be(AnalyticsConstants.SortBy.Date);
        productViewsCriteria.EventNames.Should().Equal("view_item");
        productViewsCriteria.DimensionNames.Should().Equal("itemId", "itemName");

        foreach (var criteria in analytics.ReceivedSearchCriteria)
        {
            criteria.Take.Should().Be(5);
            criteria.From.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromMinutes(5));
            var filter = criteria.DimensionFilters.Should().ContainSingle().Subject;
            filter.DimensionName.Should().Be(AnalyticsConstants.UserDimensions.SessionKind);
            filter.Values.Should().Equal(AnalyticsConstants.SessionKinds.Self);
        }
    }

    [Fact]
    public async Task Run_FeatureQuery_NoRows_ReturnsWarning()
    {
        var result = await CreateService(new FakeAnalyticsDiagnosticsService(), new FakeAnalyticsService()).RunAsync(StoreId, includeLiveData: true);

        var check = result.Checks[^1];
        check.Stage.Should().Be(ModuleConstants.DiagnosticsStages.FeatureQuery);
        check.Status.Should().Be(Statuses.Warning);
        check.Message.Should().Contain("No processed rows yet").And.Contain("24–48 hours");
    }

    [Fact]
    public async Task Run_FeatureQuery_Unconfigured_ReturnsWarning()
    {
        var analytics = new FakeAnalyticsService { Configured = false };

        var result = await CreateService(new FakeAnalyticsDiagnosticsService(), analytics).RunAsync(StoreId, includeLiveData: true);

        var check = result.Checks[^1];
        check.Status.Should().Be(Statuses.Warning);
        check.Message.Should().Contain($"not configured for store '{StoreId}'");
        analytics.ReceivedSearchCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_FeatureQuery_Exception_ReturnsFailed()
    {
        var result = await CreateService(new FakeAnalyticsDiagnosticsService(), new ThrowingAnalyticsService()).RunAsync(StoreId, includeLiveData: true);

        var check = result.Checks[^1];
        check.Stage.Should().Be(ModuleConstants.DiagnosticsStages.FeatureQuery);
        check.Status.Should().Be(Statuses.Failed);
        check.Message.Should().Be("Feature query failed.");
        check.Detail.Should().Contain("GA exploded");
    }

    private static SalesRepAnalyticsDiagnosticsService CreateService(IAnalyticsDiagnosticsService diagnostics, IAnalyticsService analytics)
    {
        return new SalesRepAnalyticsDiagnosticsService(Optional(diagnostics), Optional(analytics));
    }

    private static TestOptionalDependency<T> Optional<T>(T instance) where T : class
    {
        var services = new ServiceCollection();
        if (instance != null)
        {
            services.AddSingleton(instance);
        }

        return new TestOptionalDependency<T>(services.BuildServiceProvider());
    }

    private sealed class FakeAnalyticsDiagnosticsService : IAnalyticsDiagnosticsService
    {
        private static readonly string[] _moduleStages =
        [
            Stages.Configuration,
            Stages.Credentials,
            Stages.ApiAccess,
            Stages.CustomDimensions,
            Stages.ReportCompatibility,
            Stages.Realtime,
            Stages.ProcessedData,
        ];

        public string ReceivedStoreId { get; private set; }

        public AnalyticsDiagnosticsRequest ReceivedRequest { get; private set; }

        public Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, AnalyticsDiagnosticsRequest request)
        {
            ReceivedStoreId = storeId;
            ReceivedRequest = request;

            var result = new AnalyticsDiagnosticsResult();
            foreach (var stage in _moduleStages)
            {
                result.Checks.Add(new AnalyticsDiagnosticsCheck { Stage = stage, Status = Statuses.Passed, Message = stage });
            }

            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingAnalyticsService : IAnalyticsService
    {
        public Task<bool> IsConfiguredAsync(string storeId) => Task.FromResult(true);

        public Task<AnalyticsEventSearchResult> SearchEventsAsync(AnalyticsEventSearchCriteria criteria)
            => throw new InvalidOperationException("GA exploded");

        public Task<IList<AnalyticsEventSummary>> GetEventSummariesAsync(AnalyticsEventSummaryCriteria criteria)
            => throw new InvalidOperationException("GA exploded");
    }
}
