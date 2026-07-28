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
/// seed real orders and memberships into in-memory SQLite and assert the assigned / ordering / new customer counters.
/// "Ordering customers" derives only from the rep's own orders within the organizations they serve; "new customers"
/// derives from customer assignment dates (the rep's membership creation dates), independent of orders.
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
        // Assignment dates drive "new customers": org-1 assigned last year, org-2 assigned this year.
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _mar2025);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-2", _feb2026);
        SeedOrder(ctx, "o1a", "org-1", _mar2025); // org-1 ordered last year…
        SeedOrder(ctx, "o1b", "org-1", _feb2026); // …and this year
        SeedOrder(ctx, "o2", "org-2", _feb2026);  // org-2 ordered this year
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
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(1);      // only org-2 was assigned in 2026

        var lastYear = counts.GetProperty("lastYear");
        lastYear.GetProperty("orderingCustomers").GetInt32().Should().Be(1); // only org-1 ordered in 2025
        lastYear.GetProperty("newCustomers").GetInt32().Should().Be(1);      // org-1 was assigned in 2025

        var yoy = counts.GetProperty("yoy");
        yoy.GetProperty("orderingCustomersChange").GetInt32().Should().Be(1);            // 2 - 1
        yoy.GetProperty("orderingCustomersChangePercent").GetDecimal().Should().Be(100m);
        yoy.GetProperty("newCustomersChange").GetInt32().Should().Be(0);                 // 1 - 1
        yoy.GetProperty("newCustomersChangePercent").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Counts_NewCustomers_CountedByAssignmentDate_NotFirstOrder()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        // Both customers first ordered LAST year but were assigned to the rep only THIS year: "new customers" must
        // follow the assignment date, not the first-order date (a long-standing customer newly assigned is "new").
        SeedOrder(ctx, "o1", "org-1", _mar2025);
        SeedOrder(ctx, "o2", "org-2", _mar2025);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _feb2026);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-2", _feb2026);

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts {
                ytd:      period({{Ytd}}) { orderingCustomers newCustomers }
                lastYear: period({{LastYear}}) { orderingCustomers newCustomers }
              } }
              """,
            userId: rep.UserId);

        var counts = Stats(json);
        var ytd = counts.GetProperty("ytd");
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(2);      // both assigned in 2026 (first-order would give 0)
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(0); // neither ordered in 2026

        var lastYear = counts.GetProperty("lastYear");
        lastYear.GetProperty("newCustomers").GetInt32().Should().Be(0);      // neither assigned in 2025 (first-order would give 2)
        lastYear.GetProperty("orderingCustomers").GetInt32().Should().Be(2); // both ordered in 2025
    }

    [Fact]
    public async Task Counts_ScopeToSingleOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _feb2026);
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
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(1); // org-1 assigned in 2026
    }

    [Fact]
    public async Task Counts_ExcludesOrdersNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-1", _feb2026);
        await ctx.SetMembershipAssignmentDateAsync(rep.UserId, "org-2", _feb2026);
        SeedOrder(ctx, "mine", "org-1", _feb2026);                                      // the rep's own order
        SeedOrder(ctx, "foreign", "org-2", _feb2026, createdByUserId: "another-rep");   // foreign order in a served org

        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts {
                ytd: period({{Ytd}}) { orderingCustomers newCustomers }
              } }
              """,
            userId: rep.UserId);

        // Data-isolation invariant: org-2's only order is a foreign rep's, so it must not count as "ordering" for this
        // rep (order counters are creator-scoped). It still counts as "new" — org-2 is genuinely assigned to this rep —
        // which shows the two counters are computed independently (assignments vs the rep's own orders).
        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(1); // only org-1 (the rep's own order)
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(2);      // both assigned to the rep in 2026
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

    [Fact]
    public async Task Counts_WithUnrecognizedFilter_FailsClosed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "o1", "org-1", _feb2026);

        // The default customer-segment resolver has no segments → any segment name fails closed (zeroed counters),
        // never "count every customer".
        var json = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerCounts {
                ytd: period({{Ytd}}, filter: "vip") { orderingCustomers newCustomers }
              } }
              """,
            userId: rep.UserId);

        var ytd = Stats(json).GetProperty("ytd");
        ytd.GetProperty("orderingCustomers").GetInt32().Should().Be(0);
        ytd.GetProperty("newCustomers").GetInt32().Should().Be(0);
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
