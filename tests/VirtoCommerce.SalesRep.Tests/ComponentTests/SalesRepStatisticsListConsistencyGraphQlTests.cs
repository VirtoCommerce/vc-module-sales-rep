using System;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// Guards the invariant behind the shared filter-rule design: for the SAME resolved status filter, the orders LIST
/// (<c>salesRepOrders</c>, via the Orders search service) and the order STATISTICS
/// (<c>salesRepCustomerOrderStatistics</c>, via direct aggregation) must report the same number of orders — no
/// "list empty but count non-zero", and vice versa. Both read paths resolve statuses through the one
/// <c>ISalesRepOrderFilterRuleResolver</c> (its ApplyListFilterAsync / ApplyStatisticsFilterAsync), so this holds even
/// for a composite (1:many) status. All seeded orders are non-cancelled / non-prototype so the two paths' base exclusions
/// agree (statistics intentionally drop cancelled/prototype; the list is unfiltered by that).
/// </summary>
[Trait("Category", "Component")]
public class SalesRepStatisticsListConsistencyGraphQlTests
{
    private static readonly DateTime _feb2026 = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Ytd = "from: \"2026-01-01T00:00:00Z\", to: \"2026-07-11T00:00:00Z\"";

    [Theory]
    [InlineData(null, 3)]            // no status filter → all three orders
    [InlineData("New", 2)]           // 1:1 status → the two New orders
    [InlineData("Inactive", 1)]      // composite status (→ Cancelled + Failed) → the one Failed order
    public async Task ListTotalCount_MatchesStatisticsCount_ForSameStatusFilter(string statusName, int expected)
    {
        // The composite "Inactive" row exercises a 1:many rule, a project-override of the real resolver; the "New"
        // and no-filter rows ride the real default 1:1 statuses it also exposes.
        using var ctx = SalesRepTestContext.Create(OrderFilterRuleOverride.WithCompositeInactiveStatus);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, "n1", "org-1", _feb2026, status: "New");
        SeedOrder(ctx, "n2", "org-1", _feb2026, status: "New");
        SeedOrder(ctx, "f1", "org-1", _feb2026, status: "Failed");

        // Same unified 'filters' argument on both the list and the statistics.
        var filterArg = statusName == null ? "" : $", filter: \"{statusName}\"";

        var listJson = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepOrders(organizationId: "org-1"{{filterArg}}) { totalCount } }
              """,
            userId: rep.UserId);

        var statsJson = await ctx.ExecuteGraphQlAsync(
            $$"""
              query { salesRepCustomerOrderStatistics(organizationId: "org-1", currencyCode: "USD") {
                p: period({{Ytd}}{{filterArg}}) { count } } }
              """,
            userId: rep.UserId);

        var listTotal = SalesRepTestContext.Node(listJson, "salesRepOrders").GetProperty("totalCount").GetInt32();
        var statsCount = SalesRepTestContext.Node(statsJson, "salesRepCustomerOrderStatistics").GetProperty("p").GetProperty("count").GetInt32();

        listTotal.Should().Be(expected);
        statsCount.Should().Be(expected);
        statsCount.Should().Be(listTotal, "the list and statistics must agree for the same resolved status filter");
    }

    private static void SeedOrder(SalesRepTestContext ctx, string id, string org, DateTime createdDate, string status)
    {
        using var db = ctx.NewOrderDbContext();
        db.Add(new CustomerOrderEntity
        {
            Id = id,
            Number = id,
            OrganizationId = org,
            CustomerId = ctx.LastCreatedRepUserId,
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
            Status = status,
            Currency = "USD",
            Total = 100m,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }
}
