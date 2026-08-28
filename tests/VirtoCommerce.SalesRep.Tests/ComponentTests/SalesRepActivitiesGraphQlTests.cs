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
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using AnalyticsConstants = VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.ModuleConstants;

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
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _apr, count: 5, "org-1", sessionKind: AnalyticsConstants.SessionKinds.Impersonated);

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
            filters[AnalyticsConstants.UserDimensions.SessionKind].Should().Equal(AnalyticsConstants.SessionKinds.Self);
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
    public async Task Activities_DeepSkip_IsClamped()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepActivities(categories: [\"logins\"], skip: 100000, take: 20) { totalCount items { type } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Skip is clamped before the aggregator turns it into a per-category Take, so no source is asked for 100k rows.
        analytics.ReceivedSearchCriteria.Should().ContainSingle(x => x.Take == ModuleConstants.Activities.MaxSkip + 20);
    }

    // ---- helpers ----

    private static JsonElement Connection(string json)
        => SalesRepTestContext.Node(json, "salesRepActivities");

    private static List<(string Category, int Count)> CategoryCounts(JsonElement connection)
        => connection.GetProperty("categoryCounts").EnumerateArray()
            .Select(x => (x.GetProperty("category").GetString(), x.GetProperty("count").GetInt32()))
            .ToList();

    private static void SeedOrder(
        SalesRepTestContext ctx, string id, string org, DateTime createdDate, string createdByUserId = null)
    {
        using var db = ctx.NewOrderDbContext();
        db.Add(new CustomerOrderEntity
        {
            Id = id,
            Number = id,
            OrganizationId = org,
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
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
        SalesRepTestContext ctx, string id, string code, string name, string imageUrl = null)
    {
        var seedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var db = ctx.NewCatalogDbContext();

        if (!db.Set<CatalogEntity>().Any(x => x.Id == SalesRepTestContext.TestCatalogId))
        {
            db.Add(new CatalogEntity { Id = SalesRepTestContext.TestCatalogId, Name = "Test Catalog", DefaultLanguage = "en-US", CreatedDate = seedDate, ModifiedDate = seedDate });
        }

        var item = new ItemEntity
        {
            Id = id,
            Code = code,
            Name = name,
            CatalogId = SalesRepTestContext.TestCatalogId,
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
