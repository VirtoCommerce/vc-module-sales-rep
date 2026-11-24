using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using VirtoCommerce.AnalyticsModule.Core.Models;
using VirtoCommerce.AnalyticsModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services.Activities;
using Xunit;
using PlatformEventNames = VirtoCommerce.SalesRep.Core.ModuleConstants.Analytics.PlatformEventNames;

namespace VirtoCommerce.SalesRep.Tests.UnitTests;

/// <summary>
/// The hub's tracked figures read from the platform's own analytics store when the store chose it, and from Google
/// Analytics (absent here, so empty) when it did not.
/// </summary>
[Trait("Category", "Unit")]
public class TrackedActivityFromPlatformAnalyticsTests
{
    private const string StoreId = "B2B-store";
    private static readonly DateTime _noon = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeEventStore _eventStore = new();
    private readonly FakePlatformAnalytics _platformAnalytics;

    public TrackedActivityFromPlatformAnalyticsTests()
    {
        _platformAnalytics = new FakePlatformAnalytics(_eventStore);
    }

    [Fact]
    public async Task SearchTerms_FromThePlatform_AreCountedAndSortedAsAsked()
    {
        _eventStore.Add(PlatformEventNames.Search, "bolt", _noon.AddMinutes(-50));
        _eventStore.Add(PlatformEventNames.Search, "bolt", _noon.AddMinutes(-40));
        _eventStore.Add(PlatformEventNames.Search, "toner", _noon.AddMinutes(-5));
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-1", _noon);

        var service = CreateInsightsService();
        var byCount = await service.GetSearchTermsAsync(CreateInsightsCriteria(ModuleConstants.Insights.Sort.Count));
        var byDate = await service.GetSearchTermsAsync(CreateInsightsCriteria(ModuleConstants.Insights.Sort.Date));

        byCount.Select(x => (x.Term, x.Count)).Should().Equal(("bolt", 2), ("toner", 1));
        byCount[0].LastSearchedDate.Should().Be(_noon.AddMinutes(-40));
        byDate.Select(x => x.Term).Should().Equal("toner", "bolt");
    }

    [Fact]
    public async Task BrowsedProducts_FromThePlatform_CarryTheCodeTheViewsAndTheLastView()
    {
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-1", _noon.AddMinutes(-30));
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-1", _noon.AddMinutes(-10));
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-2", _noon.AddMinutes(-20));

        var products = await CreateInsightsService().GetBrowsedProductsAsync(CreateInsightsCriteria(ModuleConstants.Insights.Sort.Count));

        products.Select(x => (x.Code, x.ViewCount)).Should().Equal(("SKU-1", 2), ("SKU-2", 1));
        products[0].LastViewedDate.Should().Be(_noon.AddMinutes(-10));
    }

    [Fact]
    public async Task Insights_LeaveOutRepsImpersonatedSessionsAndOtherOrganizations()
    {
        _eventStore.Add(PlatformEventNames.Search, "customer term", _noon);
        _eventStore.Add(PlatformEventNames.Search, "rep term", _noon, userId: "rep-1");
        _eventStore.Add(PlatformEventNames.Search, "on behalf term", _noon, sessionKind: ModuleConstants.Analytics.SessionKinds.Impersonated);
        _eventStore.Add(PlatformEventNames.Search, "other organization term", _noon, organizationId: "org-2");

        var terms = await CreateInsightsService().GetSearchTermsAsync(CreateInsightsCriteria(ModuleConstants.Insights.Sort.Count));

        terms.Select(x => x.Term).Should().Equal("customer term");
    }

    [Fact]
    public async Task Insights_WhenTheStoreReadsGoogleAnalytics_DoNotTouchThePlatformStore()
    {
        _platformAnalytics.IsSelected = false;
        _eventStore.Add(PlatformEventNames.Search, "bolt", _noon);

        var terms = await CreateInsightsService().GetSearchTermsAsync(CreateInsightsCriteria(ModuleConstants.Insights.Sort.Count));

        terms.Should().BeEmpty();
        _eventStore.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task Summary_FromThePlatform_HasVisitsTheLastSearchAndTheLastViewedProduct()
    {
        _eventStore.Add(PlatformEventNames.SessionStart, null, _noon.AddDays(-2));
        _eventStore.Add(PlatformEventNames.SessionStart, null, _noon.AddHours(-1));
        _eventStore.Add(PlatformEventNames.Search, "bolt", _noon.AddMinutes(-30));
        _eventStore.Add(PlatformEventNames.Search, "toner", _noon.AddMinutes(-20));
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-1", _noon.AddMinutes(-10));

        var summary = await CreateActivityService().GetSummaryAsync(new SalesRepCustomerActivityCriteria { StoreId = StoreId, OrganizationId = "org-1" });

        summary.IsAnalyticsAvailable.Should().BeTrue();
        summary.VisitsCount.Should().Be(2);
        summary.LastWebLogin.Should().Be(_noon.AddHours(-1));
        summary.LastSearchTerm.Should().Be("toner");
        summary.LastSearchedDate.Should().Be(_noon.AddMinutes(-20));
        summary.LastViewedProduct.Code.Should().Be("SKU-1");
        summary.LastViewedDate.Should().Be(_noon.AddMinutes(-10));
    }

    [Fact]
    public async Task Summary_FromThePlatform_WithNoActivity_IsAvailableAndEmpty()
    {
        var summary = await CreateActivityService().GetSummaryAsync(new SalesRepCustomerActivityCriteria { StoreId = StoreId, OrganizationId = "org-1" });

        summary.IsAnalyticsAvailable.Should().BeTrue();
        summary.VisitsCount.Should().Be(0);
        summary.LastWebLogin.Should().BeNull();
        summary.LastSearchTerm.Should().BeNull();
        summary.LastViewedProduct.Should().BeNull();
    }

    [Fact]
    public async Task Feed_FromThePlatform_ReturnsExactRowsNewestFirstWithTheWholeCount()
    {
        for (var minute = 1; minute <= 5; minute++)
        {
            _eventStore.Add(PlatformEventNames.Search, $"term {minute}", _noon.AddMinutes(-minute));
        }

        var result = await CreateActivitySource().SearchAsync(CreateFeedCriteria(ModuleConstants.Activities.Categories.Searches, take: 2, skip: 1));

        result.TotalCount.Should().Be(5);
        result.EventCount.Should().Be(5);
        result.IsAnalyticsAvailable.Should().BeTrue();
        result.Results.Select(x => x.SearchTerm).Should().Equal("term 2", "term 3");
        result.Results.Should().OnlyContain(x =>
            x.Precision == ModuleConstants.Activities.Precision.Exact
            && x.Category == ModuleConstants.Activities.Categories.Searches
            && x.Type == ModuleConstants.Activities.Types.Search
            && x.OrganizationId == "org-1"
            && x.Count == 1);
    }

    [Fact]
    public async Task Feed_ProductViews_CarryTheCodeAndTheTrackedName()
    {
        _eventStore.Add(PlatformEventNames.ViewItem, "SKU-1", _noon, properties: new Dictionary<string, object>
        {
            ["items"] = JArray.Parse("[{\"item_id\":\"SKU-1\",\"item_name\":\"Eyebolt\"}]"),
        });

        var result = await CreateActivitySource().SearchAsync(CreateFeedCriteria(ModuleConstants.Activities.Categories.ProductViews, take: 5, skip: 0));

        var row = result.Results.Should().ContainSingle().Subject;
        row.ProductCode.Should().Be("SKU-1");
        row.ProductName.Should().Be("Eyebolt");
        row.SearchTerm.Should().BeNull();
    }

    [Fact]
    public async Task Feed_Logins_AreTheVisitsOfTheOrganization()
    {
        _eventStore.Add(PlatformEventNames.SessionStart, null, _noon);
        _eventStore.Add(PlatformEventNames.Search, "bolt", _noon);

        var result = await CreateActivitySource().SearchAsync(CreateFeedCriteria(ModuleConstants.Activities.Categories.Logins, take: 5, skip: 0));

        result.Results.Should().ContainSingle().Which.Type.Should().Be(ModuleConstants.Activities.Types.Login);
    }

    [Fact]
    public async Task Availability_IsTrueForAStoreThatReadsThePlatform_AndFallsBackOtherwise()
    {
        var availability = new TrackedSalesRepAnalyticsAvailability(
            NoGoogleAnalytics(),
            NullLogger<SalesRepAnalyticsAvailability>.Instance,
            _platformAnalytics);

        (await availability.IsConfiguredAsync(StoreId)).Should().BeTrue();

        _platformAnalytics.IsSelected = false;

        (await availability.IsConfiguredAsync(StoreId)).Should().BeFalse();
    }

    [Fact]
    public async Task Criteria_PinCustomersOwnSessionsAndExcludeTheRepsWhoServeTheOrganizations()
    {
        var platformAnalytics = new SalesRepPlatformAnalytics(
            new StubOptionalDependency<IAnalyticsEventSearchService>(_eventStore),
            storeService: null,
            new FakeOrganizationAccessService("rep-1", "rep-2"));

        var criteria = await platformAnalytics.CreateCriteriaAsync(StoreId, ["org-1"], PlatformEventNames.Search, _noon.AddDays(-7), _noon);

        criteria.StoreId.Should().Be(StoreId);
        criteria.OrganizationIds.Should().Equal("org-1");
        criteria.Names.Should().Equal(PlatformEventNames.Search);
        criteria.SessionKinds.Should().Equal(ModuleConstants.Analytics.SessionKinds.Self);
        criteria.ExcludedUserIds.Should().BeEquivalentTo("rep-1", "rep-2");
        criteria.From.Should().Be(_noon.AddDays(-7));
        criteria.To.Should().Be(_noon);
    }

    [Fact]
    public async Task Criteria_RefuseAReadThatNamesNoOrganization()
    {
        var platformAnalytics = new SalesRepPlatformAnalytics(
            new StubOptionalDependency<IAnalyticsEventSearchService>(_eventStore),
            storeService: null,
            new FakeOrganizationAccessService());

        var act = () => platformAnalytics.CreateCriteriaAsync(StoreId, [], PlatformEventNames.Search, null, null);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchService_IsNotOffered_WhenTheAnalyticsModuleIsNotInstalled()
    {
        var platformAnalytics = new SalesRepPlatformAnalytics(
            new StubOptionalDependency<IAnalyticsEventSearchService>(null),
            storeService: null,
            new FakeOrganizationAccessService());

        (await platformAnalytics.GetSearchServiceAsync(StoreId)).Should().BeNull();
    }

    private TrackedSalesRepCustomerInsightsService CreateInsightsService()
    {
        return new TrackedSalesRepCustomerInsightsService(
            NoGoogleAnalytics(),
            new TrackedSalesRepAnalyticsAvailability(NoGoogleAnalytics(), NullLogger<SalesRepAnalyticsAvailability>.Instance, _platformAnalytics),
            new SalesRepAnalyticsScope(),
            _platformAnalytics);
    }

    private TrackedSalesRepCustomerActivityService CreateActivityService()
    {
        return new TrackedSalesRepCustomerActivityService(
            NoGoogleAnalytics(),
            new TrackedSalesRepAnalyticsAvailability(NoGoogleAnalytics(), NullLogger<SalesRepAnalyticsAvailability>.Instance, _platformAnalytics),
            new SalesRepAnalyticsScope(),
            NullLogger<SalesRepCustomerActivityService>.Instance,
            _platformAnalytics);
    }

    private TrackedSalesRepActivitySource CreateActivitySource()
    {
        return new TrackedSalesRepActivitySource(
            NoGoogleAnalytics(),
            new SalesRepAnalyticsScope(),
            NullLogger<AnalyticsSalesRepActivitySource>.Instance,
            _platformAnalytics);
    }

    private static OptionalDependencyManager<IAnalyticsService> NoGoogleAnalytics()
    {
        return new OptionalDependencyManager<IAnalyticsService>(new ServiceCollection().BuildServiceProvider());
    }

    private static SalesRepCustomerInsightsCriteria CreateInsightsCriteria(string sortBy)
    {
        return new SalesRepCustomerInsightsCriteria { StoreId = StoreId, OrganizationIds = ["org-1"], SortBy = sortBy, Take = 5 };
    }

    private static SalesRepActivitySearchCriteria CreateFeedCriteria(string category, int take, int skip)
    {
        return new SalesRepActivitySearchCriteria
        {
            StoreId = StoreId,
            OrganizationIds = ["org-1"],
            Categories = [category],
            Take = take,
            Skip = skip,
        };
    }

    /// <summary>Scopes reads the way the real one does, with "rep-1" as the rep who serves every organization.</summary>
    private sealed class FakePlatformAnalytics(FakeEventStore eventStore) : ISalesRepPlatformAnalytics
    {
        public bool IsSelected { get; set; } = true;

        public Task<IAnalyticsEventSearchService> GetSearchServiceAsync(string storeId)
        {
            return Task.FromResult<IAnalyticsEventSearchService>(IsSelected ? eventStore : null);
        }

        public Task<AnalyticsEventSearchCriteria> CreateCriteriaAsync(string storeId, IList<string> organizationIds, string eventName, DateTime? from, DateTime? to)
        {
            return Task.FromResult(new AnalyticsEventSearchCriteria
            {
                StoreId = storeId,
                OrganizationIds = organizationIds,
                Names = [eventName],
                SessionKinds = [ModuleConstants.Analytics.SessionKinds.Self],
                ExcludedUserIds = ["rep-1"],
                From = from,
                To = to,
            });
        }
    }

    /// <summary>Applies the criteria the way the analytics module documents them, over rows held in memory.</summary>
    private sealed class FakeEventStore : IAnalyticsEventSearchService
    {
        private readonly List<AnalyticsEvent> _events = [];

        public int ReadCount { get; private set; }

        public void Add(
            string name,
            string subject,
            DateTime occurredAt,
            string userId = "customer-1",
            string organizationId = "org-1",
            string sessionKind = ModuleConstants.Analytics.SessionKinds.Self,
            IDictionary<string, object> properties = null)
        {
            _events.Add(new AnalyticsEvent
            {
                Name = name,
                Subject = subject,
                OccurredAt = occurredAt,
                StoreId = StoreId,
                UserId = userId,
                OrganizationId = organizationId,
                SessionKind = sessionKind,
                Properties = properties ?? new Dictionary<string, object>(),
            });
        }

        public Task<bool> IsAvailableAsync(string storeId)
        {
            return Task.FromResult(true);
        }

        public Task<AnalyticsEventSearchResult> SearchAsync(AnalyticsEventSearchCriteria criteria)
        {
            var matching = Filter(criteria).OrderByDescending(x => x.OccurredAt).ToList();

            return Task.FromResult(new AnalyticsEventSearchResult
            {
                TotalCount = matching.Count,
                Results = matching.Skip(criteria.Skip).Take(criteria.Take).ToList(),
            });
        }

        public Task<IList<AnalyticsSubjectSummary>> GetSubjectSummariesAsync(AnalyticsEventSearchCriteria criteria, string sortBy, int take)
        {
            var summaries = Filter(criteria)
                .Where(x => x.Subject != null)
                .GroupBy(x => x.Subject)
                .Select(x => new AnalyticsSubjectSummary { Subject = x.Key, Count = x.Count(), LastOccurredAt = x.Max(e => e.OccurredAt) });

            summaries = sortBy == AnalyticsSummarySort.Date
                ? summaries.OrderByDescending(x => x.LastOccurredAt)
                : summaries.OrderByDescending(x => x.Count).ThenByDescending(x => x.LastOccurredAt);

            return Task.FromResult<IList<AnalyticsSubjectSummary>>(summaries.Take(take).ToList());
        }

        public Task<IList<AnalyticsNameCount>> GetNameCountsAsync(AnalyticsEventSearchCriteria criteria)
        {
            throw new NotSupportedException();
        }

        private IEnumerable<AnalyticsEvent> Filter(AnalyticsEventSearchCriteria criteria)
        {
            ReadCount++;

            return _events.Where(x =>
                x.StoreId == criteria.StoreId
                && criteria.Names.Contains(x.Name)
                && criteria.OrganizationIds.Contains(x.OrganizationId)
                && criteria.SessionKinds.Contains(x.SessionKind)
                && !criteria.ExcludedUserIds.Contains(x.UserId)
                && (!criteria.WithSubjectOnly || x.Subject != null)
                && (criteria.From == null || x.OccurredAt >= criteria.From)
                && (criteria.To == null || x.OccurredAt <= criteria.To));
        }
    }

    private sealed class FakeOrganizationAccessService(params string[] repUserIds) : ISalesRepOrganizationAccessService
    {
        public Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null)
        {
            return Task.FromResult<IList<OrganizationMembership>>(repUserIds
                .Select(x => new OrganizationMembership { UserId = x, OrganizationId = organizationIds?.FirstOrDefault() })
                .ToList());
        }

        public Task<bool> ServesOrganizationAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<bool> ServesAllOrganizationsAsync(string userId, IList<string> organizationIds) => throw new NotSupportedException();

        public Task<IList<string>> GetServedOrganizationIdsAsync(string userId) => throw new NotSupportedException();

        public Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId) => throw new NotSupportedException();
    }

    private sealed class StubOptionalDependency<T>(T value) : IOptionalDependency<T>
    {
        public T Value { get; } = value;

        public bool HasValue => Value != null;
    }
}
