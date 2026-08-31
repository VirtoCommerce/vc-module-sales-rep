using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;
using SalesRepConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepActivities</c> X-API query (VCST-5337 activity feed): seed real
/// orders, memberships, catalog products and a fake analytics pool, and assert the merged feed, category counts,
/// product resolution and — above all — the data-isolation invariant (a rep never sees a foreign org's activity).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepActivitiesGraphQlTests
{
    private static readonly DateTime _jan = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _feb = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar = new(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr = new(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

    private const string AllFields =
        "totalCount categoryCounts { category count } items { category type occurredAt precision count organizationId organizationName " +
        "orderId orderNumber orderStatus orderStatusDisplayValue orderTotal { amount currency { code } } " +
        "searchTerm productId productCode productName productImageUrl }";

    [Fact]
    public async Task Activities_MergesOrdersAndCustomers_NewestFirst()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _jan);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-2", _mar);
        SeedOrder(ctx, "o1", "org-1", _feb);
        SeedOrder(ctx, "o2", "org-1", _apr);

        var json = await ctx.ExecuteGraphQlAsync($"query {{ salesRepActivities(cultureName:\"en-US\") {{ {AllFields} }} }}", userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(4);

        var counts = CategoryCounts(connection);
        counts.Should().Equal(("orders", 2), ("customers", 2), ("searches", 0), ("productViews", 0), ("logins", 0));

        var items = connection.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("type").GetString())
            .Should().Equal("orderPlaced", "customerAssigned", "orderPlaced", "customerAssigned");
        items.Select(x => x.GetProperty("occurredAt").GetDateTime().ToUniversalTime())
            .Should().BeInDescendingOrder();
        items.Should().OnlyContain(x => x.GetProperty("precision").GetString() == "exact" && x.GetProperty("count").GetInt32() == 1);

        var order = items[0];
        order.GetProperty("orderNumber").GetString().Should().Be("o2");
        order.GetProperty("orderStatus").GetString().Should().Be("New");
        order.GetProperty("orderStatusDisplayValue").GetString().Should().Be("New (en-US)"); // culture reached the localizer
        order.GetProperty("orderTotal").GetProperty("amount").GetDecimal().Should().Be(100m);
        order.GetProperty("organizationId").GetString().Should().Be("org-1");
        order.GetProperty("organizationName").GetString().Should().Be("org-1"); // batch-loaded from the member service

        var assignment = items[1];
        assignment.GetProperty("category").GetString().Should().Be("customers");
        assignment.GetProperty("organizationId").GetString().Should().Be("org-2");
        assignment.GetProperty("orderId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Activities_CategoryFilterAndPaging()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "o1", "org-1", _jan);
        SeedOrder(ctx, "o2", "org-1", _feb);
        SeedOrder(ctx, "o3", "org-1", _mar);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"orders\"], take: 1, skip: 1) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(3); // the selected category only
        // The unselected tabs keep their own badges instead of collapsing to 0.
        CategoryCounts(connection).Should().Equal(("orders", 3), ("customers", 1), ("searches", 0), ("productViews", 0), ("logins", 0));

        var items = connection.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("orderNumber").GetString().Should().Be("o2"); // newest first, second page row
    }

    [Fact]
    public async Task Activities_PeriodBoundsApply()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _jan);
        SeedOrder(ctx, "in-range", "org-1", _feb);
        SeedOrder(ctx, "too-late", "org-1", _apr);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(period: {{ from: \"2026-02-01T00:00:00Z\", to: \"2026-03-01T00:00:00Z\" }}) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(1);
        var items = connection.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(1);
        items[0].GetProperty("orderNumber").GetString().Should().Be("in-range"); // the january assignment is filtered out too
    }

    [Fact]
    public async Task Activities_AnalyticsRows_MappedResolvedAndScoped()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _jan);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-2", _jan);
        SeedProduct(ctx, "prod-1", "CODE-1", "Catalog Pump", imageUrl: "https://img/pump.png");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 3, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _apr, count: 1, "org-2");
        // Foreign-org and impersonated events sit in the pool but MUST stay invisible (server-side GA filters).
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _apr, count: 5, "org-foreign",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "leak"));
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _apr, count: 5, "org-1", sessionKind: SalesRepConstants.Analytics.SessionKinds.Impersonated);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"searches\", \"productViews\", \"logins\"], storeId: \"B2B-store\", cultureName:\"en-US\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(3);
        CategoryCounts(connection).Should().Equal(("orders", 0), ("customers", 2), ("searches", 1), ("productViews", 1), ("logins", 1));

        var items = connection.GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("type").GetString()).Should().Equal("login", "productView", "search");
        items.Should().OnlyContain(x => x.GetProperty("precision").GetString() == "hour");

        var login = items[0];
        login.GetProperty("organizationId").GetString().Should().Be("org-2");

        var productView = items[1];
        productView.GetProperty("count").GetInt32().Should().Be(3);
        productView.GetProperty("productCode").GetString().Should().Be("CODE-1");
        productView.GetProperty("productId").GetString().Should().Be("prod-1");
        productView.GetProperty("productName").GetString().Should().Be("Catalog Pump"); // catalog name wins over the GA snapshot
        productView.GetProperty("productImageUrl").GetString().Should().Be("https://img/pump.png");

        var search = items[2];
        search.GetProperty("searchTerm").GetString().Should().Be("pumps");
        search.GetProperty("count").GetInt32().Should().Be(2);

        json.Should().NotContain("leak");

        // One GA read per fetched category — its count reuses the fetch's TotalCount, no extra Take=0 read.
        analytics.ReceivedSearchCriteria.Should().HaveCount(3);
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.Take > 0);

        // Every analytics read carries the mandatory scope: own sessions only, and only the rep's organizations.
        foreach (var criteria in analytics.ReceivedSearchCriteria)
        {
            var filters = criteria.DimensionFilters.ToDictionary(x => x.DimensionName, x => x.Values);
            filters[AnalyticsConstants.UserDimensions.SessionKind].Should().Equal(SalesRepConstants.Analytics.SessionKinds.Self);
            filters[AnalyticsConstants.UserDimensions.OrganizationId].Should().BeEquivalentTo("org-1", "org-2");
        }
    }

    [Fact]
    public async Task Activities_UnresolvableProductCode_StillReturnsCode()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _feb, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "GONE-1"), (AnalyticsConstants.Dimensions.ItemName, "Deleted Pump")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"productViews\"]) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var row = Connection(json).GetProperty("items").EnumerateArray().Single();
        row.GetProperty("productCode").GetString().Should().Be("GONE-1");
        row.GetProperty("productName").GetString().Should().Be("Deleted Pump"); // GA snapshot survives
        row.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("productImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Activities_AnalyticsAbsent_ZeroCountsNoErrors()
    {
        using var ctx = SalesRepTestContext.Create(); // no IAnalyticsService registered = module absent
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"searches\", \"productViews\", \"logins\"]) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(0);
        CategoryCounts(connection).Should().Equal(("orders", 0), ("customers", 1), ("searches", 0), ("productViews", 0), ("logins", 0));
        connection.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Activities_DataIsolation_OmittedOrganization_ScopesToServedOrgs()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var otherRep = await ctx.CreateRepAsync("Other", "Rep", "other@test.com", "org-2");
        SeedOrder(ctx, "foreign-order", "org-2", _feb, createdByUserId: otherRep.UserId);
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 1, "org-2",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "foreign-search"));

        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _jan);
        SeedOrder(ctx, "own-order", "org-1", _mar);

        var json = await ctx.ExecuteGraphQlAsync($"query {{ salesRepActivities {{ {AllFields} }} }}", userId: rep.UserId);

        // The must-not-appear assertions: nothing from org-2 leaks into Jane's feed.
        json.Should().NotContain("foreign-order");
        json.Should().NotContain("foreign-search");
        json.Should().NotContain("org-2");

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(2); // own order + own assignment
        analytics.ReceivedSearchCriteria.Should().OnlyContain(criteria =>
            criteria.DimensionFilters.Single(f => f.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId).Values.Single() == "org-1");
    }

    [Fact]
    public async Task Activities_ForeignOrganizationId_ReturnsNull()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "leak", "org-2", _feb, createdByUserId: "someone-else");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _feb, count: 1, "org-2");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepActivities(organizationId: \"org-2\") { totalCount items { orderNumber } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepActivities\":null");
        analytics.ReceivedSearchCriteria.Should().BeEmpty(); // rejected before any analytics read
    }

    [Fact]
    public async Task Activities_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync("query { salesRepActivities { totalCount } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task Activities_SkipPastThePagingWindow_ReturnsNoRowsAndReadsNothing()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _feb, count: 3, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"logins\"], skip: {ModuleConstants.Activities.MaxSkip + 1}, take: 20) {{ totalCount categoryCounts {{ category count }} items {{ type }} }} }}",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");

        var connection = Connection(json);
        // No rows rather than the window's last page relabelled: a caller cannot tell a repeated page from data.
        connection.GetProperty("items").EnumerateArray().Should().BeEmpty();
        // The counters still describe the whole set, so a client can see the feed is longer than it can page.
        CategoryCounts(connection).Single(x => x.Category == "logins").Count.Should().Be(1);
        connection.GetProperty("totalCount").GetInt32().Should().Be(1);

        // And the read is a counting pass, not a page-sized fetch of everything up to the skip.
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.Take == 0);
    }

    [Fact]
    public async Task Activities_WithoutCategoryCounts_ReadsOnlyTheRequestedCategory()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "own-order", "org-1", _mar);
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _apr, count: 1, "org-1");

        // The badges are what makes a request count every category. A caller that does not select them -- the
        // storefront loads them separately -- must not wait on Google to render a database-backed tab.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepActivities(categories: [\"orders\"]) { totalCount items { type orderNumber } } }",
            userId: rep.UserId);

        var connection = Connection(json);
        connection.GetProperty("totalCount").GetInt32().Should().Be(1);
        connection.GetProperty("items").EnumerateArray().Single().GetProperty("type").GetString().Should().Be("orderPlaced");
        analytics.ReceivedSearchCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Activities_WithCategoryCounts_StillCountsEveryCategory()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "own-order", "org-1", _mar);
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _apr, count: 1, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepActivities(categories: [\"orders\"]) { totalCount categoryCounts { category count } } }",
            userId: rep.UserId);

        // Selecting the badges is what buys the unrequested categories their counting pass.
        CategoryCounts(Connection(json)).Should().Equal(("orders", 1), ("customers", 1), ("searches", 0), ("productViews", 0), ("logins", 1));
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.Take == 0);
    }

    // One search journey fires two GA events — 'search' from the header dropdown, 'view_search_results' when
    // the results page opens — and GA returns a row per event name. Counting both would show the same search
    // twice and disagree with the insights list, which counts 'search' alone.
    [Fact]
    public async Task Activities_OneSearchJourney_IsOneRow()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 3, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "coffee"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewSearchResults, _feb, count: 3, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "coffee"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"searches\"]) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        var row = connection.GetProperty("items").EnumerateArray().Single();
        row.GetProperty("searchTerm").GetString().Should().Be("coffee");
        row.GetProperty("count").GetInt32().Should().Be(3);

        // The badge counts what the list shows.
        CategoryCounts(connection).Single(x => x.Category == "searches").Count.Should().Be(1);
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.EventNames.Count == 1);
    }

    // storeId means two different things at once: which store's orders count, and which analytics property the
    // tracked categories are read from. Omitting it leaves both unscoped.
    [Fact]
    public async Task Activities_StoreId_ScopesOrdersAndNamesTheAnalyticsProperty()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "own-store", "org-1", _feb);
        SeedOrder(ctx, "other-store", "org-1", _mar, storeId: "Other-store");

        var scoped = Connection(await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"orders\"], storeId: \"B2B-store\") {{ {AllFields} }} }}",
            userId: rep.UserId));

        scoped.GetProperty("items").EnumerateArray().Single()
            .GetProperty("orderNumber").GetString().Should().Be("own-store");
        analytics.ReceivedSearchCriteria.Should().NotBeEmpty();
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.StoreId == "B2B-store");

        var unscoped = Connection(await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"orders\"]) {{ {AllFields} }} }}",
            userId: rep.UserId));

        unscoped.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("orderNumber").GetString())
            .Should().BeEquivalentTo("other-store", "own-store");
    }

    // Take and Skip are the caller's, but the cost of serving them is not: a take past the cap, or a negative
    // skip, must reach the sources as the bounded values rather than as asked.
    [Fact]
    public async Task Activities_TakeBeyondTheCapAndNegativeSkip_AreBounded()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _feb, count: 1, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"logins\"], take: 999, skip: -5) {{ {AllFields} }} }}",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // One fetched category pages natively, so the source is asked for the page itself.
        analytics.ReceivedSearchCriteria
            .Should().ContainSingle(x => x.Take == SalesRepActivitiesQuery.MaxTake && x.Skip == 0);
    }

    [Fact]
    public async Task Activities_PagingWindow_BoundsWhatEverySourceIsAskedFor()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // The "All" view: every category fetches, so the rows a request costs are the window x categories. The
        // window is Skip + Take rounded up to the paging bucket, so the deepest page asks for 500 + 50 -> 600.
        const int worstCaseWindow = 600;

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(skip: {ModuleConstants.Activities.MaxSkip}, take: 50) {{ totalCount items {{ type }} }} }}",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        analytics.ReceivedSearchCriteria.Should().OnlyContain(x => x.Take <= worstCaseWindow);
        analytics.ReceivedSearchCriteria.Sum(x => x.Take).Should().BeLessThanOrEqualTo(3 * worstCaseWindow);
    }

    [Fact]
    public async Task Activities_ProductCodeOnlyInAnotherCatalog_DoesNotResolve()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // The code exists ONLY outside the store's catalog. A code is unique within a catalog, not across them, so
        // an unscoped lookup would answer with this product and show the rep a foreign catalog's name and image.
        SeedProduct(ctx, "prod-foreign", "CODE-1", "Foreign Pump", imageUrl: "https://img/foreign.png",
            catalogId: "other-catalog");

        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"productViews\"], storeId: \"B2B-store\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var row = Connection(json).GetProperty("items").EnumerateArray().Single();
        row.GetProperty("productCode").GetString().Should().Be("CODE-1");
        row.GetProperty("productName").GetString().Should().Be("GA Pump"); // the tracked name, not the foreign catalog's
        row.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("productImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Activities_AnalyticsRowWithoutHourBucket_IsDroppedFromTheCategoryCountToo()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _feb, count: 2, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        // GA returned a row whose hour bucket is unusable: it cannot be placed on a time-ordered feed.
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, occurredAt: null, count: 7, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "no-bucket"));

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"searches\"]) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var connection = Connection(json);
        var items = connection.GetProperty("items").EnumerateArray().ToList();

        // The badge counts the rows the feed can show, so it cannot advertise a row the list can never render.
        items.Should().ContainSingle().Which.GetProperty("searchTerm").GetString().Should().Be("pumps");
        CategoryCounts(connection).Single(x => x.Category == "searches").Count.Should().Be(1);
        connection.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Activities_ProductCodeInSeveralCatalogs_ResolvesToNothingWhenUnscoped()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // The same code in two catalogs. With no storeId there is no catalog to pick by, so neither product may
        // answer — putting one of them on screen would be a coin flip the rep cannot see.
        SeedProduct(ctx, "prod-store", "CODE-1", "Catalog Pump", imageUrl: "https://img/pump.png");
        SeedProduct(ctx, "prod-foreign", "CODE-1", "Foreign Pump", imageUrl: "https://img/foreign.png",
            catalogId: "other-catalog");

        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"productViews\"]) {{ {AllFields} }} }}",
            userId: rep.UserId);

        var row = Connection(json).GetProperty("items").EnumerateArray().Single();
        row.GetProperty("productCode").GetString().Should().Be("CODE-1");
        row.GetProperty("productName").GetString().Should().Be("GA Pump"); // the tracked name survives
        row.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("productImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Activities_ProductCodeInSeveralCatalogs_StoreCatalogStillWins()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        SeedProduct(ctx, "prod-store", "CODE-1", "Catalog Pump", imageUrl: "https://img/pump.png");
        SeedProduct(ctx, "prod-foreign", "CODE-1", "Foreign Pump", imageUrl: "https://img/foreign.png",
            catalogId: "other-catalog");

        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);

        // With a storeId the catalog disambiguates, so the ambiguity guard must not cost the normal path anything.
        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepActivities(categories: [\"productViews\"], storeId: \"B2B-store\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var row = Connection(json).GetProperty("items").EnumerateArray().Single();
        row.GetProperty("productId").GetString().Should().Be("prod-store");
        row.GetProperty("productName").GetString().Should().Be("Catalog Pump");
        row.GetProperty("productImageUrl").GetString().Should().Be("https://img/pump.png");
    }

    // ---- helpers ----

    private static JsonElement Connection(string json)
        => SalesRepTestContext.Node(json, "salesRepActivities");

    private static List<(string Category, int Count)> CategoryCounts(JsonElement connection)
        => connection.GetProperty("categoryCounts").EnumerateArray()
            .Select(x => (x.GetProperty("category").GetString(), x.GetProperty("count").GetInt32()))
            .ToList();

    private static void SeedOrder(
        SalesRepTestContext ctx, string id, string org, DateTime createdDate, string createdByUserId = null,
        string storeId = "B2B-store")
    {
        using var db = ctx.NewOrderDbContext();
        db.Add(new CustomerOrderEntity
        {
            Id = id,
            Number = id,
            OrganizationId = org,
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Status = "New",
            Currency = "USD",
            Total = 100m,
            IsPrototype = false,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }

    internal static void SeedProduct(
        SalesRepTestContext ctx, string id, string code, string name, string imageUrl = null, string catalogId = null)
    {
        var seedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        catalogId ??= SalesRepTestContext.TestCatalogId;

        using var db = ctx.NewCatalogDbContext();

        if (!db.Set<CatalogEntity>().Any(x => x.Id == catalogId))
        {
            db.Add(new CatalogEntity { Id = catalogId, Name = catalogId, DefaultLanguage = "en-US", CreatedDate = seedDate, ModifiedDate = seedDate });
        }

        var item = new ItemEntity
        {
            Id = id,
            Code = code,
            Name = name,
            CatalogId = catalogId,
            IsActive = true,
            CreatedDate = seedDate,
            ModifiedDate = seedDate,
        };

        if (imageUrl != null)
        {
            item.Images = new ObservableCollection<ImageEntity>(
            [
                new ImageEntity { Id = $"{id}-img", Url = imageUrl, SortOrder = 0, CreatedDate = seedDate, ModifiedDate = seedDate },
            ]);
        }

        db.Add(item);
        db.SaveChanges();
    }
}
