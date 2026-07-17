using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepCustomerCartStatistics</c> X-API query: seed real carts into
/// in-memory SQLite, execute real GraphQL through the real scoped schema / MediatR handler / the real
/// <c>CustomerCartStatisticsService</c>, and assert the aggregated, currency-converted numbers. Money fields are
/// MoneyType, so numeric values are asserted via <c>{ amount }</c>. The default cart-kind service maps the built-in
/// "project" kind to cart type "Wishlist".
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerCartStatisticsGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr2026 = new(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar2025 = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";
    private const string LastYear = "from: \"2025-01-01T00:00:00Z\", to: \"2026-01-01T00:00:00Z\"";

    private const string Wishlist = ModuleConstants.CartType.Wishlist;

    [Fact]
    public async Task Cart_ProjectKind_CountsWishlistsOnly_WithComparison()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "w1", "org-1", 100m, _feb2026, type: Wishlist);
        SeedCart(ctx, "w2", "org-1", 200m, _apr2026, type: Wishlist);
        SeedCart(ctx, "wprev", "org-1", 40m, _mar2025, type: Wishlist);   // previous year
        SeedCart(ctx, "c1", "org-1", 999m, _feb2026, type: null);          // normal cart → excluded by "project" kind

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                currencyCode
                projects:  period({{Ytd}}, filter: "project") { total { amount } count average { amount } lastCartDate }
                allCarts:  period({{Ytd}}) { count }
                yoy: comparison(current: { {{Ytd}} }, previous: { {{LastYear}} }, filter: "project") {
                  countChange countChangePercent totalChange { amount }
                }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("currencyCode").GetString().Should().Be("USD");

        var projects = stats.GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(300m); // 100 + 200 (normal cart 999 excluded)
        projects.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(projects, "average").Should().Be(150m);
        projects.GetProperty("lastCartDate").GetDateTime().Should().Be(_apr2026);

        stats.GetProperty("allCarts").GetProperty("count").GetInt32().Should().Be(3); // both wishlists + the normal cart

        var yoy = stats.GetProperty("yoy");
        yoy.GetProperty("countChange").GetInt32().Should().Be(1);            // 2 (YTD) - 1 (prev year)
        yoy.GetProperty("countChangePercent").GetDecimal().Should().Be(100m);
        MoneyAmount(yoy, "totalChange").Should().Be(260m);                    // 300 - 40
    }

    [Fact]
    public async Task Cart_FoldsMultipleCurrencies_IntoRequestedUsd()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "w-usd", "org-1", 100m, _feb2026, type: Wishlist, currency: "USD");
        SeedCart(ctx, "w-eur", "org-1", 100m, _feb2026, type: Wishlist, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                projects: period({{Ytd}}, filter: "project") { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var projects = Stats(json).GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(225m); // 100 USD + 100 EUR * 1.25
        projects.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(projects, "average").Should().Be(112.5m);
    }

    [Fact]
    public async Task Cart_ExcludesDeletedCarts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "live", "org-1", 100m, _feb2026, type: Wishlist);
        SeedCart(ctx, "dead", "org-1", 999m, _feb2026, type: Wishlist, isDeleted: true);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                projects: period({{Ytd}}, filter: "project") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var projects = Stats(json).GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(100m); // the deleted 999 never counts
        projects.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_KindFilter_UnrecognizedName_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "w1", "org-1", 100m, _feb2026, type: Wishlist);

        // An unrecognized kind resolves to an empty filter → zeros, not every cart.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                bogus: period({{Ytd}}, filter: "DoesNotExist") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var bogus = Stats(json).GetProperty("bogus");
        MoneyAmount(bogus, "total").Should().Be(0m);
        bogus.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Cart_ScopesByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "b2b", "org-1", 100m, _feb2026, type: Wishlist, storeId: "B2B-store");
        SeedCart(ctx, "other", "org-1", 500m, _feb2026, type: Wishlist, storeId: "OtherStore");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", storeId: "B2B-store", currencyCode: "USD") {
                projects: period({{Ytd}}, filter: "project") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var projects = Stats(json).GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(100m);
        projects.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_AggregatesAllServedOrgs_WhenOrganizationIdOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedCart(ctx, "w1", "org-1", 100m, _feb2026, type: Wishlist);
        SeedCart(ctx, "w2", "org-2", 200m, _feb2026, type: Wishlist);
        SeedCart(ctx, "w3", "org-3", 999m, _feb2026, type: Wishlist); // not served → excluded

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(currencyCode: "USD") {
                projects: period({{Ytd}}, filter: "project") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var projects = Stats(json).GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(300m); // org-1 + org-2; org-3 excluded
        projects.GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Cart_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "leak", "org-2", 100m, _feb2026, type: Wishlist);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-2", currencyCode: "USD") { projects: period({{Ytd}}, filter: "project") { count } } }
              """,
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerCartStatistics\":null");
    }

    [Fact]
    public async Task Cart_ExcludesCartsNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedCart(ctx, "mine", "org-1", 100m, _feb2026, type: Wishlist);                                       // created by the rep
        SeedCart(ctx, "foreign", "org-1", 999m, _feb2026, type: Wishlist, createdByUserId: "another-rep-user"); // same served org, ANOTHER rep

        // Data-isolation invariant: a rep sees only projects they created — a foreign rep's project in the same
        // served organization must never contribute.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") {
                projects: period({{Ytd}}, filter: "project") { total { amount } count } } }
              """,
            userId: rep.UserId);

        var projects = Stats(json).GetProperty("projects");
        MoneyAmount(projects, "total").Should().Be(100m); // only the rep's own project; the foreign 999 excluded
        projects.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Cart_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            $$"""
              query { salesRepCustomerCartStatistics(organizationId:"org-1", currencyCode: "USD") { projects: period({{Ytd}}, filter: "project") { count } } }
              """);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    // ---- helpers ----

    private static JsonElement Stats(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty("salesRepCustomerCartStatistics").Clone();
    }

    private static decimal MoneyAmount(JsonElement parent, string field)
        => parent.GetProperty(field).GetProperty("amount").GetDecimal();

    private static void SeedCart(
        SalesRepTestContext ctx, string id, string org, decimal total, DateTime createdDate,
        string type = null, string status = null, string currency = "USD", string storeId = "B2B-store",
        bool isDeleted = false, string createdByUserId = null)
    {
        using var db = ctx.NewCartDbContext();
        db.Add(new ShoppingCartEntity
        {
            Id = id,
            Name = id,
            CheckoutId = id, // [Required] on ShoppingCartEntity
            OrganizationId = org,
            // A rep builds a project/cart for the customer, so the cart's CustomerId is the rep's user id; default
            // the creator to the test's rep. Pass createdByUserId to simulate another rep's cart.
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Type = type,
            Status = status,
            Currency = currency,
            Total = total,
            IsDeleted = isDeleted,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }
}
