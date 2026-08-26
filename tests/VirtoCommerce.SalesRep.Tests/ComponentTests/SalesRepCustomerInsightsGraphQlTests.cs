using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepCustomerInsights</c> X-API query (VCST-5337 customer insights):
/// top/recent search terms and browsed products from the (fake) analytics service, product resolution from the
/// catalog, lazy per-collection fetches shared with dataAsOf, and the same organization authorization plus
/// null-when-unavailable semantics as the activity summary. The organizationId argument is optional: omitted, the
/// scope is all the rep's assigned organizations (same resolution as salesRepActivities).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerInsightsGraphQlTests
{
    private static readonly DateTime _feb = new(2026, 2, 10, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar = new(2026, 3, 10, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr = new(2026, 4, 10, 13, 0, 0, DateTimeKind.Utc);

    private const string TermFields = "term count lastSearchedDate";
    private const string ProductFields = "productId name sku imageUrl slug viewCount lastViewedDate";

    [Fact]
    public async Task Insights_SearchTerms_DefaultSortIsTopByCount_CountsSearchEventOnly()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 3, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 4, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "valves"));
        // The paired event of the same search action MUST NOT be double-counted.
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewSearchResults, _mar, count: 100, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights(organizationId: \"org-1\", storeId: \"B2B-store\") {{ dataAsOf searchTerms {{ {TermFields} }} }} }}",
            userId: rep.UserId);

        var insights = Insights(json);
        insights.GetProperty("dataAsOf").ValueKind.Should().Be(JsonValueKind.Null); // count-mode aggregate rows carry no dates

        var terms = insights.GetProperty("searchTerms").EnumerateArray().ToList();
        terms.Select(x => (x.GetProperty("term").GetString(), x.GetProperty("count").GetInt32()))
            .Should().Equal(("pumps", 5), ("valves", 4));
        terms.Should().OnlyContain(x => x.GetProperty("lastSearchedDate").ValueKind == JsonValueKind.Null);

        var criteria = analytics.ReceivedSearchCriteria.Should().ContainSingle().Subject;
        criteria.EventNames.Should().Equal(AnalyticsConstants.EventNames.Search); // 'search' alone, never 'view_search_results'
        criteria.SortBy.Should().Be(AnalyticsConstants.SortBy.Count);
        criteria.Take.Should().Be(50); // bounded count-mode page
    }

    [Fact]
    public async Task Insights_SearchTerms_SortDate_AggregatesBucketsNewestFirst_AndDataAsOf()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 3, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 4, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "valves"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights(organizationId: \"org-1\") {{ dataAsOf searchTerms(sort: \"date\") {{ {TermFields} }} }} }}",
            userId: rep.UserId);

        var insights = Insights(json);
        insights.GetProperty("dataAsOf").GetDateTime().ToUniversalTime().Should().Be(_apr);

        var terms = insights.GetProperty("searchTerms").EnumerateArray().ToList();
        terms.Select(x => x.GetProperty("term").GetString()).Should().Equal("valves", "pumps"); // most recent first
        terms[0].GetProperty("count").GetInt32().Should().Be(4);
        terms[0].GetProperty("lastSearchedDate").GetDateTime().ToUniversalTime().Should().Be(_apr);
        terms[1].GetProperty("count").GetInt32().Should().Be(5); // summed across the pumped hour buckets
        terms[1].GetProperty("lastSearchedDate").GetDateTime().ToUniversalTime().Should().Be(_mar);

        var criteria = analytics.ReceivedSearchCriteria.Should().ContainSingle().Subject; // dataAsOf shares the fetch
        criteria.SortBy.Should().Be(AnalyticsConstants.SortBy.Date);
        criteria.Take.Should().Be(200); // bounded page of the newest hour buckets
    }

    [Fact]
    public async Task Insights_BrowsedProducts_CountSort_ResolvesCatalogAndFallsBackToCode()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SalesRepActivitiesGraphQlTests.SeedProduct(ctx, "prod-1", "CODE-1", "Catalog Pump", slug: "catalog-pump", imageUrl: "https://img/pump.png");

        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _feb, count: 2, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 3, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 4, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "GONE-1"), (AnalyticsConstants.Dimensions.ItemName, "Deleted Pump")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights(organizationId: \"org-1\", storeId: \"B2B-store\", cultureName: \"en-US\") {{ browsedProducts {{ {ProductFields} }} }} }}",
            userId: rep.UserId);

        var products = Insights(json).GetProperty("browsedProducts").EnumerateArray().ToList();
        products.Should().HaveCount(2);

        var resolved = products[0]; // top by view count: 2 + 3 = 5
        resolved.GetProperty("productId").GetString().Should().Be("prod-1");
        resolved.GetProperty("sku").GetString().Should().Be("CODE-1");
        resolved.GetProperty("name").GetString().Should().Be("Catalog Pump"); // catalog name wins over the GA snapshot
        resolved.GetProperty("slug").GetString().Should().Be("catalog-pump");
        resolved.GetProperty("imageUrl").GetString().Should().Be("https://img/pump.png");
        resolved.GetProperty("viewCount").GetInt32().Should().Be(5);
        resolved.GetProperty("lastViewedDate").ValueKind.Should().Be(JsonValueKind.Null); // count-mode rows carry no dates

        var unresolved = products[1];
        unresolved.GetProperty("productId").GetString().Should().Be("GONE-1"); // code fallback keeps the field non-null
        unresolved.GetProperty("sku").GetString().Should().Be("GONE-1");
        unresolved.GetProperty("name").GetString().Should().Be("Deleted Pump"); // GA snapshot survives
        unresolved.GetProperty("slug").ValueKind.Should().Be(JsonValueKind.Null);
        unresolved.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
        unresolved.GetProperty("viewCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Insights_BrowsedProducts_SortDate_OrdersByRecency_AndDataAsOf()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _apr, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "Pump")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _feb, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-2"), (AnalyticsConstants.Dimensions.ItemName, "Valve")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 4, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-2"), (AnalyticsConstants.Dimensions.ItemName, "Valve")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights(organizationId: \"org-1\") {{ dataAsOf browsedProducts(sort: \"date\") {{ {ProductFields} }} }} }}",
            userId: rep.UserId);

        var insights = Insights(json);
        insights.GetProperty("dataAsOf").GetDateTime().ToUniversalTime().Should().Be(_apr);

        var products = insights.GetProperty("browsedProducts").EnumerateArray().ToList();
        products.Select(x => x.GetProperty("sku").GetString()).Should().Equal("CODE-1", "CODE-2"); // most recent first
        products[1].GetProperty("viewCount").GetInt32().Should().Be(5); // summed across hour buckets
        products[1].GetProperty("lastViewedDate").GetDateTime().ToUniversalTime().Should().Be(_mar);
    }

    [Fact]
    public async Task Insights_Take_DefaultsToFive_AndClampsTo1To20()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        for (var i = 1; i <= 25; i++)
        {
            analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: i, "org-1",
                dimensions: (AnalyticsConstants.Dimensions.SearchTerm, $"term-{i:00}"));
        }

        var defaultJson = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { searchTerms { term count } } }",
            userId: rep.UserId);
        var defaultTerms = Insights(defaultJson).GetProperty("searchTerms").EnumerateArray().ToList();
        defaultTerms.Should().HaveCount(5);
        defaultTerms.Select(x => x.GetProperty("count").GetInt32()).Should().Equal(25, 24, 23, 22, 21); // top by count

        var clampedHighJson = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { searchTerms(take: 100) { term } } }",
            userId: rep.UserId);
        Insights(clampedHighJson).GetProperty("searchTerms").GetArrayLength().Should().Be(20);

        var clampedLowJson = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { searchTerms(take: -3) { term } } }",
            userId: rep.UserId);
        Insights(clampedLowJson).GetProperty("searchTerms").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Insights_SelectingOnlySearchTerms_DoesNotFireTheProductReport()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.ItemId, "CODE-1"));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { searchTerms { term } } }",
            userId: rep.UserId);

        Insights(json).GetProperty("searchTerms").GetArrayLength().Should().Be(1);
        var criteria = analytics.ReceivedSearchCriteria.Should().ContainSingle().Subject;
        criteria.EventNames.Should().Equal(AnalyticsConstants.EventNames.Search);
    }

    [Fact]
    public async Task Insights_DataAsOfListedFirst_SharesTheCollectionFetch()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));

        // dataAsOf listed BEFORE the collection: the memoized per-arguments fetch keeps this a single analytics read.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { dataAsOf searchTerms(sort: \"date\", take: 3) { term } } }",
            userId: rep.UserId);

        var insights = Insights(json);
        insights.GetProperty("dataAsOf").GetDateTime().ToUniversalTime().Should().Be(_mar);
        insights.GetProperty("searchTerms").GetArrayLength().Should().Be(1);
        analytics.ReceivedSearchCriteria.Should().ContainSingle();
    }

    [Fact]
    public async Task Insights_PeriodBoundsApply()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "too-early"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "in-range"));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\", period: { from: \"2026-03-01T00:00:00Z\", to: \"2026-05-01T00:00:00Z\" }) { searchTerms { term } } }",
            userId: rep.UserId);

        var terms = Insights(json).GetProperty("searchTerms").EnumerateArray().ToList();
        terms.Select(x => x.GetProperty("term").GetString()).Should().Equal("in-range");
    }

    [Fact]
    public async Task Insights_DataIsolation_ForeignAndImpersonatedEventsNeverLeak()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "own-term"));
        // Foreign-org and impersonated events sit in the pool but MUST stay invisible (server-side GA filters).
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 9, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "leak-term"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _apr, count: 9, "org-2",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "LEAK-CODE"), (AnalyticsConstants.Dimensions.ItemName, "Leak Product")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 9, "org-1", sessionKind: AnalyticsConstants.SessionKinds.Impersonated,
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "impersonated-term"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights(organizationId: \"org-1\") {{ dataAsOf searchTerms(sort: \"date\") {{ {TermFields} }} browsedProducts(sort: \"date\") {{ {ProductFields} }} }} }}",
            userId: rep.UserId);

        json.Should().NotContain("leak");
        json.Should().NotContain("impersonated-term");

        var insights = Insights(json);
        insights.GetProperty("searchTerms").EnumerateArray().Select(x => x.GetProperty("term").GetString()).Should().Equal("own-term");
        insights.GetProperty("browsedProducts").GetArrayLength().Should().Be(0);
        insights.GetProperty("dataAsOf").GetDateTime().ToUniversalTime().Should().Be(_mar); // the foreign april events never count

        // Every analytics read carries the mandatory scope: own sessions only, and only the requested organization.
        analytics.ReceivedSearchCriteria.Should().NotBeEmpty();
        foreach (var criteria in analytics.ReceivedSearchCriteria)
        {
            var filters = criteria.DimensionFilters.ToDictionary(x => x.DimensionName, x => x.Values);
            filters[AnalyticsConstants.UserDimensions.SessionKind].Should().Equal(AnalyticsConstants.SessionKinds.Self);
            filters[AnalyticsConstants.UserDimensions.OrganizationId].Should().Equal("org-1");
        }
    }

    [Fact]
    public async Task Insights_OmittedOrganizationId_AggregatesAcrossAssignedOrganizations()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 3, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 4, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "valves"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights {{ searchTerms {{ {TermFields} }} }} }}",
            userId: rep.UserId);

        var terms = Insights(json).GetProperty("searchTerms").EnumerateArray().ToList();
        terms.Select(x => (x.GetProperty("term").GetString(), x.GetProperty("count").GetInt32()))
            .Should().Equal(("pumps", 5), ("valves", 4)); // both assigned orgs' events aggregate together

        var criteria = analytics.ReceivedSearchCriteria.Should().ContainSingle().Subject;
        var organizationFilter = criteria.DimensionFilters.Single(x => x.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId);
        organizationFilter.Values.Should().BeEquivalentTo("org-1", "org-2"); // the full assigned-org scope
    }

    [Fact]
    public async Task Insights_OmittedOrganizationId_ForeignAndImpersonatedEventsNeverLeak()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "own-term"));
        // Another rep's org and impersonated events sit in the pool but MUST stay outside the rep-wide scope.
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 9, "org-3",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "leak-term"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _apr, count: 9, "org-3",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "LEAK-CODE"), (AnalyticsConstants.Dimensions.ItemName, "Leak Product")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 9, "org-1", sessionKind: AnalyticsConstants.SessionKinds.Impersonated,
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "impersonated-term"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerInsights {{ dataAsOf searchTerms(sort: \"date\") {{ {TermFields} }} browsedProducts(sort: \"date\") {{ {ProductFields} }} }} }}",
            userId: rep.UserId);

        json.Should().NotContain("leak");
        json.Should().NotContain("impersonated-term");

        var insights = Insights(json);
        insights.GetProperty("searchTerms").EnumerateArray().Select(x => x.GetProperty("term").GetString()).Should().Equal("own-term");
        insights.GetProperty("browsedProducts").GetArrayLength().Should().Be(0);
        insights.GetProperty("dataAsOf").GetDateTime().ToUniversalTime().Should().Be(_mar); // the foreign april events never count

        // Every analytics read carries the mandatory scope: own sessions only, and only the rep's assigned organizations.
        analytics.ReceivedSearchCriteria.Should().NotBeEmpty();
        foreach (var criteria in analytics.ReceivedSearchCriteria)
        {
            var filters = criteria.DimensionFilters.ToDictionary(x => x.DimensionName, x => x.Values);
            filters[AnalyticsConstants.UserDimensions.SessionKind].Should().Equal(AnalyticsConstants.SessionKinds.Self);
            filters[AnalyticsConstants.UserDimensions.OrganizationId].Should().BeEquivalentTo("org-1", "org-2");
        }
    }

    [Fact]
    public async Task Insights_OmittedOrganizationId_NonRepCaller_ReturnsNull()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights { searchTerms { term } } }",
            userId: "not-a-rep");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerInsights\":null");
        analytics.ReceivedSearchCriteria.Should().BeEmpty(); // empty rep scope short-circuits before any read
    }

    [Fact]
    public async Task Insights_AnalyticsAbsent_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create(); // no IAnalyticsService registered = module absent
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\") { dataAsOf searchTerms { term } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerInsights\":null");
    }

    [Fact]
    public async Task Insights_AnalyticsUnconfigured_ReturnsNull()
    {
        var analytics = new FakeAnalyticsService { Configured = false };
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-1\", storeId: \"B2B-store\") { searchTerms { term } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerInsights\":null");
        analytics.ReceivedSearchCriteria.Should().BeEmpty(); // unconfigured short-circuits before any read
    }

    [Fact]
    public async Task Insights_ForeignOrganizationId_ReturnsNull()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "foreign"));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerInsights(organizationId: \"org-2\") { searchTerms { term } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerInsights\":null");
        analytics.ReceivedSearchCriteria.Should().BeEmpty(); // rejected before any analytics read
    }

    [Fact]
    public async Task Insights_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCustomerInsights { dataAsOf } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    private static JsonElement Insights(string json)
        => SalesRepTestContext.Node(json, "salesRepCustomerInsights");
}
