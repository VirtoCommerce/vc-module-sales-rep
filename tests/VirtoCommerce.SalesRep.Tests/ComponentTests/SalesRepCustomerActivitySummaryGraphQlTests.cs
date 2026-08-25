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
/// End-to-end component tests for the <c>salesRepCustomerActivitySummary</c> X-API query (customer-activity widget):
/// createdOn from the organization record, login/search/product figures from the (fake) analytics service, product
/// resolution from the catalog, and the same organization authorization as the statistics queries.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerActivitySummaryGraphQlTests
{
    private static readonly DateTime _feb = new(2026, 2, 10, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar = new(2026, 3, 10, 13, 0, 0, DateTimeKind.Utc);

    private const string AllFields =
        "createdOn lastWebLogin visitsCount lastSearchTerm isAnalyticsConfigured " +
        "lastViewedProduct { code productId name slug imageUrl }";

    [Fact]
    public async Task Summary_ReturnsAnalyticsFiguresAndResolvedProduct()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SalesRepActivitiesGraphQlTests.SeedProduct(ctx, "prod-1", "CODE-1", "Catalog Pump", slug: "catalog-pump", imageUrl: "https://img/pump.png");

        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _feb, count: 2, "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _mar, count: 3, "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Search, _mar, count: 1, "org-1",
            dimensions: (AnalyticsConstants.Dimensions.SearchTerm, "pumps"));
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "CODE-1"), (AnalyticsConstants.Dimensions.ItemName, "GA Pump")]);
        // Foreign-org noise that must not affect org-1's figures.
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _mar, count: 100, "org-other");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerActivitySummary(organizationId: \"org-1\", storeId: \"B2B-store\", cultureName: \"en-US\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var summary = Summary(json);
        summary.GetProperty("isAnalyticsConfigured").GetBoolean().Should().BeTrue();
        summary.GetProperty("createdOn").GetDateTime().Should().BeAfter(DateTime.MinValue); // stamped by the member service on seed
        summary.GetProperty("visitsCount").GetInt32().Should().Be(5); // 2 + 3, org-1 logins only
        summary.GetProperty("lastWebLogin").GetDateTime().ToUniversalTime().Should().Be(_mar);
        summary.GetProperty("lastSearchTerm").GetString().Should().Be("pumps");

        var product = summary.GetProperty("lastViewedProduct");
        product.GetProperty("code").GetString().Should().Be("CODE-1");
        product.GetProperty("productId").GetString().Should().Be("prod-1");
        product.GetProperty("name").GetString().Should().Be("Catalog Pump");
        product.GetProperty("slug").GetString().Should().Be("catalog-pump");
        product.GetProperty("imageUrl").GetString().Should().Be("https://img/pump.png");

        // Every analytics read is scoped to the single organization and to the customer's own sessions.
        foreach (var filters in analytics.ReceivedSearchCriteria.Select(x => x.DimensionFilters)
                     .Concat(analytics.ReceivedSummaryCriteria.Select(x => x.DimensionFilters)))
        {
            filters.Single(x => x.DimensionName == AnalyticsConstants.UserDimensions.SessionKind).Values.Should().Equal(AnalyticsConstants.SessionKinds.Self);
            filters.Single(x => x.DimensionName == AnalyticsConstants.UserDimensions.OrganizationId).Values.Should().Equal("org-1");
        }
    }

    [Fact]
    public async Task Summary_UnresolvableProductCode_KeepsCodeAndTrackedName()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.ViewItem, _mar, count: 1, "org-1",
            dimensions: [(AnalyticsConstants.Dimensions.ItemId, "GONE-1"), (AnalyticsConstants.Dimensions.ItemName, "Deleted Pump")]);

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerActivitySummary(organizationId: \"org-1\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var product = Summary(json).GetProperty("lastViewedProduct");
        product.GetProperty("code").GetString().Should().Be("GONE-1");
        product.GetProperty("name").GetString().Should().Be("Deleted Pump");
        product.GetProperty("productId").ValueKind.Should().Be(JsonValueKind.Null);
        product.GetProperty("slug").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Summary_AnalyticsAbsent_ReturnsCreatedOnOnly()
    {
        using var ctx = SalesRepTestContext.Create(); // no IAnalyticsService registered = module absent
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerActivitySummary(organizationId: \"org-1\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var summary = Summary(json);
        summary.GetProperty("isAnalyticsConfigured").GetBoolean().Should().BeFalse();
        summary.GetProperty("createdOn").ValueKind.Should().Be(JsonValueKind.String); // DB data still served
        summary.GetProperty("visitsCount").GetInt32().Should().Be(0);
        summary.GetProperty("lastWebLogin").ValueKind.Should().Be(JsonValueKind.Null);
        summary.GetProperty("lastSearchTerm").ValueKind.Should().Be(JsonValueKind.Null);
        summary.GetProperty("lastViewedProduct").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Summary_AnalyticsUnconfigured_ReportsNotConfigured()
    {
        var analytics = new FakeAnalyticsService { Configured = false };
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _mar, count: 3, "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerActivitySummary(organizationId: \"org-1\") {{ {AllFields} }} }}",
            userId: rep.UserId);

        var summary = Summary(json);
        summary.GetProperty("isAnalyticsConfigured").GetBoolean().Should().BeFalse();
        summary.GetProperty("visitsCount").GetInt32().Should().Be(0); // unconfigured short-circuits before any read
        analytics.ReceivedSummaryCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_ForeignOrganizationId_ReturnsNull()
    {
        var analytics = new FakeAnalyticsService();
        using var ctx = SalesRepTestContext.Create(services => services.AddSingleton<IAnalyticsService>(analytics));
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        analytics.AddEvent(AnalyticsConstants.EventNames.Login, _mar, count: 3, "org-2");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerActivitySummary(organizationId: \"org-2\") { visitsCount } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerActivitySummary\":null");
        analytics.ReceivedSummaryCriteria.Should().BeEmpty(); // rejected before any analytics read
    }

    [Fact]
    public async Task Summary_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCustomerActivitySummary(organizationId: \"org-1\") { visitsCount } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    private static JsonElement Summary(string json)
        => SalesRepTestContext.Node(json, "salesRepCustomerActivitySummary");
}
