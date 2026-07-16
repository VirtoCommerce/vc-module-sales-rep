using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the <c>salesRepCustomerCounts</c> X-API query (dashboard "My Customers" widget):
/// seed real orders into in-memory SQLite and assert the assigned / ordering / new customer counters, derived only
/// from the rep's own orders within the organizations they serve.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepCustomerCountsGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _mar2025 = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";
    private const string LastYear = "from: \"2025-01-01T00:00:00Z\", to: \"2026-01-01T00:00:00Z\"";

    [Fact]
    public async Task Counts_AssignedOrderingNew_WithComparison()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2"); // serves org-1, org-2
        SeedOrder(ctx, "o1a", "org-1", _mar2025); // org-1's first order (previous year)
        SeedOrder(ctx, "o1b", "org-1", _feb2026); // org-1 also ordered this year
        SeedOrder(ctx, "o2", "org-2", _feb2026);  // org-2's first & only order (this year)
        SeedOrder(ctx, "o3", "org-3", _feb2026);  // org-3 not served → never counted

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts {
                assignedCustomers
                ytd:      period({{Ytd}}) { orderingCustomers newCustomers }
                lastYear: period({{LastYear}}) { orderingCustomers newCustomers }
                yoy: comparison(current: { {{Ytd}} }, previous: { {{LastYear}} }) {
                  orderingCustomersChange orderingCustomersChangePercent newCustomersChange newCustomersChangePercent
                }
              } }
              """,
            userId: rep.UserId);

        var counts = Stats(json);
        counts.GetProperty("assignedCustomers").GetInt32().Should().Be(2); // org-1, org-2 (not org-3)

        var ytd = counts.GetProperty("ytd");
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(2); // org-1 + org-2 ordered in 2026
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(1);      // only org-2's first order is in 2026

        var lastYear = counts.GetProperty("lastYear");
        lastYear.GetProperty("orderingCustomers").GetInt32().Should().Be(1); // only org-1 ordered in 2025
        lastYear.GetProperty("newCustomers").GetInt32().Should().Be(1);      // org-1's first order is in 2025

        var yoy = counts.GetProperty("yoy");
        yoy.GetProperty("orderingCustomersChange").GetInt32().Should().Be(1);            // 2 - 1
        yoy.GetProperty("orderingCustomersChangePercent").GetDecimal().Should().Be(100m);
        yoy.GetProperty("newCustomersChange").GetInt32().Should().Be(0);                 // 1 - 1
        yoy.GetProperty("newCustomersChangePercent").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Counts_ScopeToSingleOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedOrder(ctx, "o1", "org-1", _feb2026);
        SeedOrder(ctx, "o2", "org-2", _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts(organizationId: "org-1") {
                assignedCustomers
                ytd: period({{Ytd}}) { orderingCustomers newCustomers }
              } }
              """,
            userId: rep.UserId);

        var counts = Stats(json);
        counts.GetProperty("assignedCustomers").GetInt32().Should().Be(1); // scoped to the one org
        var ytd = counts.GetProperty("ytd");
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(1);
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Counts_ExcludesOrdersNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedOrder(ctx, "mine", "org-1", _feb2026);                                      // the rep's own order
        SeedOrder(ctx, "foreign", "org-2", _feb2026, createdByUserId: "another-rep");   // foreign order in a served org

        // Data-isolation invariant: org-2 only has a foreign rep's order, so it must not count as "ordering" or "new"
        // for this rep.
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts {
                ytd: period({{Ytd}}) { orderingCustomers newCustomers }
              } }
              """,
            userId: rep.UserId);

        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(1); // only org-1 (the rep's own)
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Counts_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "leak", "org-2", _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts(organizationId: "org-2") { assignedCustomers ytd: period({{Ytd}}) { orderingCustomers } } }
              """,
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerCounts\":null");
    }

    [Fact]
    public async Task Counts_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            $$"""
              query { salesRepCustomerCounts { assignedCustomers ytd: period({{Ytd}}) { orderingCustomers } } }
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
        return root.GetProperty("data").GetProperty("salesRepCustomerCounts").Clone();
    }

    private static void SeedOrder(
        SalesRepTestContext ctx, string id, string org, DateTime createdDate,
        string storeId = "B2B-store", string createdByUserId = null)
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
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }
}
