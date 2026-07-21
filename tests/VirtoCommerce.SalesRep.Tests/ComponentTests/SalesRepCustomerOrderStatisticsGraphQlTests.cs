using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepCustomerOrderStatistics</c> X-API query (VCST-5309): seed real
/// orders into in-memory SQLite, execute real GraphQL through the real scoped schema / MediatR handler / the
/// real <c>CustomerOrderStatisticsService</c>, and assert the aggregated, currency-converted numbers exactly.
/// Money fields are MoneyType (like SalesRepOrder.total), so the numeric value is asserted via <c>{ amount }</c>.
/// The only stand-ins are the peripheral currency/store data sources (fixed rates in <c>TestGraphQlConfiguration</c>).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerOrderStatisticsGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _apr2026 = new(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _jun2026 = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar2025 = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _sep2025 = new(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _may2024 = new(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    // period/comparison ranges reused across the query strings
    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";
    private const string LastYear = "from: \"2025-01-01T00:00:00Z\", to: \"2026-01-01T00:00:00Z\"";

    [Fact]
    public async Task Statistics_ComputesYtdPreviousLifetimeAndComparison()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // org-1, all USD, B2B-store. Two excluded orders (cancelled, prototype) that must never count.
        SeedOrder(ctx, "y1", "org-1", 100m, _feb2026);
        SeedOrder(ctx, "y2", "org-1", 200m, _apr2026);
        SeedOrder(ctx, "y3", "org-1", 300m, _jun2026);
        SeedOrder(ctx, "p1", "org-1", 50m, _mar2025);
        SeedOrder(ctx, "p2", "org-1", 150m, _sep2025);
        SeedOrder(ctx, "life", "org-1", 1000m, _may2024);
        SeedOrder(ctx, "x-cancelled", "org-1", 9999m, _apr2026, isCancelled: true);
        SeedOrder(ctx, "x-prototype", "org-1", 8888m, _apr2026, isPrototype: true);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query {
                salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                  currencyCode
                  ytd:      period({{Ytd}}) { total { amount } count average { amount } lastOrderDate }
                  lastYear: period({{LastYear}}) { total { amount } count average { amount } }
                  lifetime: period { total { amount } count lastOrderDate }
                  ytdVsLastYear: comparison(current: { {{Ytd}} }, previous: { {{LastYear}} }) {
                    totalChange { amount } totalChangePercent countChange countChangePercent averageChange { amount } averageChangePercent
                  }
                }
              }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("currencyCode").GetString().Should().Be("USD");

        var ytd = stats.GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(600m);       // 100 + 200 + 300 (cancelled/prototype excluded)
        ytd.GetProperty("count").GetInt32().Should().Be(3);
        MoneyAmount(ytd, "average").Should().Be(200m);     // 600 / 3
        ytd.GetProperty("lastOrderDate").GetDateTime().Should().Be(_jun2026);

        var lastYear = stats.GetProperty("lastYear");
        MoneyAmount(lastYear, "total").Should().Be(200m);  // 50 + 150
        lastYear.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(lastYear, "average").Should().Be(100m);

        var lifetime = stats.GetProperty("lifetime");
        MoneyAmount(lifetime, "total").Should().Be(1800m); // everything except the two excluded
        lifetime.GetProperty("count").GetInt32().Should().Be(6);
        lifetime.GetProperty("lastOrderDate").GetDateTime().Should().Be(_jun2026);

        var cmp = stats.GetProperty("ytdVsLastYear");
        MoneyAmount(cmp, "totalChange").Should().Be(400m);                      // 600 - 200
        cmp.GetProperty("totalChangePercent").GetDecimal().Should().Be(200m);   // 400 / 200
        cmp.GetProperty("countChange").GetInt32().Should().Be(1);
        cmp.GetProperty("countChangePercent").GetDecimal().Should().Be(50m);
        MoneyAmount(cmp, "averageChange").Should().Be(100m);                    // 200 - 100
        cmp.GetProperty("averageChangePercent").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Statistics_FoldsMultipleCurrencies_IntoRequestedUsd()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "usd", "org-1", 100m, _feb2026, currency: "USD");
        SeedOrder(ctx, "eur", "org-1", 100m, _feb2026, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                currencyCode ytd: period({{Ytd}}) { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        // 100 USD + (100 EUR * 1.25) = 225 USD across 2 orders → AOV 112.5. Correct AOV across currencies is only
        // possible because each group's count is kept until after conversion.
        stats.GetProperty("currencyCode").GetString().Should().Be("USD");
        var ytd = stats.GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(225m);
        ytd.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(ytd, "average").Should().Be(112.5m);
    }

    [Fact]
    public async Task Statistics_FoldsMultipleCurrencies_IntoRequestedEur()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "usd", "org-1", 100m, _feb2026, currency: "USD");
        SeedOrder(ctx, "eur", "org-1", 100m, _feb2026, currency: "EUR");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "EUR") {
                currencyCode ytd: period({{Ytd}}) { total { amount } count average { amount } } } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        // (100 USD / 1.25) + 100 EUR = 80 + 100 = 180 EUR across 2 orders → AOV 90.
        stats.GetProperty("currencyCode").GetString().Should().Be("EUR");
        var ytd = stats.GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(180m);
        ytd.GetProperty("count").GetInt32().Should().Be(2);
        MoneyAmount(ytd, "average").Should().Be(90m);
    }

    [Fact]
    public async Task Statistics_DefaultsToStoreCurrency_WhenCurrencyOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "usd", "org-1", 100m, _feb2026, currency: "USD");

        // No currencyCode, but a store is given → the store's default currency (EUR in the test store double) wins.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", storeId: "B2B-store") {
                currencyCode ytd: period({{Ytd}}) { total { amount } } } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("currencyCode").GetString().Should().Be("EUR"); // store default, not the USD primary
        MoneyAmount(stats.GetProperty("ytd"), "total").Should().Be(80m);  // 100 USD / 1.25
    }

    [Fact]
    public async Task Statistics_DefaultsToPrimaryCurrency_WhenNoStoreOrCurrency()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "usd", "org-1", 100m, _feb2026, currency: "USD");

        // Neither currencyCode nor storeId → falls back to the platform primary currency (USD).
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1") { currencyCode ytd: period({{Ytd}}) { total { amount } } } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        stats.GetProperty("currencyCode").GetString().Should().Be("USD");
        MoneyAmount(stats.GetProperty("ytd"), "total").Should().Be(100m);
    }

    [Fact]
    public async Task Statistics_ScopesOrdersByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "b2b", "org-1", 100m, _feb2026, storeId: "B2B-store");
        SeedOrder(ctx, "other", "org-1", 500m, _feb2026, storeId: "OtherStore");

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", storeId: "B2B-store", currencyCode: "USD") {
                ytd: period({{Ytd}}) { total { amount } count } } }
              """,
            userId: rep.UserId);

        var ytd = Stats(json).GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(100m); // only the B2B-store order
        ytd.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Statistics_EmptyPreviousPeriod_YieldsZerosAndNullPercents()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "y1", "org-1", 100m, _feb2026); // 2026 only; no 2025 orders

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                lastYear: period({{LastYear}}) { total { amount } count average { amount } lastOrderDate }
                ytdVsLastYear: comparison(current: { {{Ytd}} }, previous: { {{LastYear}} }) {
                  totalChange { amount } totalChangePercent countChange countChangePercent averageChange { amount } averageChangePercent
                }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        var prev = stats.GetProperty("lastYear");
        MoneyAmount(prev, "total").Should().Be(0m);
        prev.GetProperty("count").GetInt32().Should().Be(0);
        MoneyAmount(prev, "average").Should().Be(0m);
        prev.GetProperty("lastOrderDate").ValueKind.Should().Be(JsonValueKind.Null);

        var cmp = stats.GetProperty("ytdVsLastYear");
        MoneyAmount(cmp, "totalChange").Should().Be(100m);                                // 100 - 0 (absolute still valid)
        cmp.GetProperty("totalChangePercent").ValueKind.Should().Be(JsonValueKind.Null);  // no ratio against zero
        cmp.GetProperty("countChange").GetInt32().Should().Be(1);
        cmp.GetProperty("countChangePercent").ValueKind.Should().Be(JsonValueKind.Null);
        cmp.GetProperty("averageChangePercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Statistics_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "leak", "org-2", 100m, _feb2026); // exists, but the rep does not serve org-2

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-2", currencyCode: "USD") { ytd: period({{Ytd}}) { total { amount } } } }
              """,
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerOrderStatistics\":null");
    }

    [Fact]
    public async Task Statistics_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") { ytd: period({{Ytd}}) { total { amount } } } }
              """);

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task Statistics_AggregatesAllServedOrgs_WhenOrganizationIdOmitted()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        // The rep serves org-1 and org-2 only.
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedOrder(ctx, "o1", "org-1", 100m, _feb2026);
        SeedOrder(ctx, "o2", "org-2", 200m, _feb2026);
        SeedOrder(ctx, "o3", "org-3", 999m, _feb2026); // org-3 is not served → must be excluded

        // No organizationId → aggregate across every organization the rep serves (org-1 + org-2), never org-3.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(currencyCode: "USD") {
                ytd: period({{Ytd}}) { total { amount } count } } }
              """,
            userId: rep.UserId);

        var ytd = Stats(json).GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(300m); // 100 (org-1) + 200 (org-2); org-3 excluded
        ytd.GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Statistics_ExcludesOrdersNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "mine", "org-1", 100m, _feb2026);                                       // created by the rep
        SeedOrder(ctx, "foreign", "org-1", 999m, _feb2026, createdByUserId: "another-rep-user"); // same served org, ANOTHER rep

        // Data-isolation invariant: a rep sees statistics only for orders they created — a foreign rep's order in the
        // very same served organization must never contribute.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                ytd: period({{Ytd}}) { total { amount } count } } }
              """,
            userId: rep.UserId);

        var ytd = Stats(json).GetProperty("ytd");
        MoneyAmount(ytd, "total").Should().Be(100m); // only the rep's own order; the foreign 999 is excluded
        ytd.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Statistics_StatusFilter_NarrowsToSelectedStatuses()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "n1", "org-1", 100m, _feb2026, status: "New");
        SeedOrder(ctx, "n2", "org-1", 200m, _feb2026, status: "New");
        SeedOrder(ctx, "f1", "org-1", 500m, _feb2026, status: "Failed"); // not cancelled → counted by default, excluded by "New"

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                all:      period({{Ytd}}) { total { amount } count }
                onlyNew:  period({{Ytd}}, filter: "New") { total { amount } count }
              } }
              """,
            userId: rep.UserId);

        var stats = Stats(json);
        MoneyAmount(stats.GetProperty("all"), "total").Should().Be(800m);     // 100 + 200 + 500
        stats.GetProperty("all").GetProperty("count").GetInt32().Should().Be(3);
        MoneyAmount(stats.GetProperty("onlyNew"), "total").Should().Be(300m); // 100 + 200 (Failed excluded)
        stats.GetProperty("onlyNew").GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Statistics_StatusFilter_ResolvesCompositeStatus()
    {
        // A composite "Inactive" → { Cancelled, Failed } rule is a project-override of the real resolver.
        using var ctx = SalesRepTestContext.Create(OrderFilterRuleOverride.WithCompositeInactiveStatus);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "n1", "org-1", 100m, _feb2026, status: "New");
        SeedOrder(ctx, "f1", "org-1", 500m, _feb2026, status: "Failed"); // part of the "Inactive" composite

        // The override status service maps the business name "Inactive" → { Cancelled, Failed } (1:many).
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                inactive: period({{Ytd}}, filter: "Inactive") { total { amount } count }
              } }
              """,
            userId: rep.UserId);

        var inactive = Stats(json).GetProperty("inactive");
        MoneyAmount(inactive, "total").Should().Be(500m); // the Failed order only
        inactive.GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Statistics_StatusFilter_UnrecognizedName_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "n1", "org-1", 100m, _feb2026, status: "New");

        // An unrecognized status name resolves to nothing → the widget must yield zeros, NOT silently count every
        // order (mirrors the orders-list fail-closed fix).
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId:"org-1", currencyCode: "USD") {
                bogus: period({{Ytd}}, filter: "DoesNotExist") { total { amount } count }
              } }
              """,
            userId: rep.UserId);

        var bogus = Stats(json).GetProperty("bogus");
        MoneyAmount(bogus, "total").Should().Be(0m);
        bogus.GetProperty("count").GetInt32().Should().Be(0);
    }

    // ---- helpers ----

    /// <summary>The <c>data.salesRepCustomerOrderStatistics</c> node, after asserting the response carries no errors.</summary>
    private static JsonElement Stats(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty("salesRepCustomerOrderStatistics").Clone();
    }

    /// <summary>Reads the numeric <c>amount</c> of a MoneyType money field (e.g. total / average / totalChange).</summary>
    private static decimal MoneyAmount(JsonElement parent, string field)
        => parent.GetProperty(field).GetProperty("amount").GetDecimal();

    private static void SeedOrder(
        SalesRepTestContext ctx, string id, string org, decimal total, DateTime createdDate,
        string currency = "USD", string storeId = "B2B-store", bool isCancelled = false, bool isPrototype = false,
        string createdByUserId = null, string status = "New")
    {
        using var db = ctx.NewOrderDbContext();
        db.Add(new CustomerOrderEntity
        {
            Id = id,
            Number = id,
            OrganizationId = org,
            // A rep-created order records the rep's user id as CustomerId; default the creator to the test's rep so
            // seeded orders count as "created by the rep". Pass createdByUserId to simulate another rep's order.
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Status = status,
            Currency = currency,
            Total = total,
            IsCancelled = isCancelled,
            IsPrototype = isPrototype,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }
}
