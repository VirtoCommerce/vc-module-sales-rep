using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

// Ordering and paging of salesRepCustomerOrders, pinned as the endpoint actually behaves. `after` is an
// offset, not an opaque cursor - SearchQuery.Map parses it with int.TryParse - so these also fix the
// consequences of that, which the storefront relies on and a caller would otherwise have to discover.
[Trait("Category", "Component")]
public class SalesRepCustomerOrdersPagingGraphQlTests
{
    private static readonly DateTime _march = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _april = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _may = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DefaultSort_IsNewestFirst()
    {
        using var ctx = await SeedThreeMonthsAsync();

        var numbers = await NumbersAsync(ctx, "salesRepCustomerOrders");

        numbers.Should().Equal("ORD-MAY", "ORD-APRIL", "ORD-MARCH");
    }

    [Theory]
    [InlineData("createdDate:desc", new[] { "ORD-MAY", "ORD-APRIL", "ORD-MARCH" })]
    [InlineData("createdDate:asc", new[] { "ORD-MARCH", "ORD-APRIL", "ORD-MAY" })]
    public async Task Sort_IsHonouredInBothDirections(string sort, string[] expected)
    {
        using var ctx = await SeedThreeMonthsAsync();

        var numbers = await NumbersAsync(ctx, $"salesRepCustomerOrders(sort:\"{sort}\")");

        numbers.Should().Equal(expected);
    }

    [Fact]
    public async Task Paging_TakesFirstAndSkipsAfterAsAnOffset()
    {
        using var ctx = await SeedThreeMonthsAsync();

        var page1 = await NumbersAsync(ctx, "salesRepCustomerOrders(first:2)");
        var page2 = await NumbersAsync(ctx, "salesRepCustomerOrders(first:2, after:\"2\")");

        page1.Should().Equal("ORD-MAY", "ORD-APRIL");
        page2.Should().Equal("ORD-MARCH");
        page1.Should().NotIntersectWith(page2);

        // An offset, not an opaque cursor: after:"1" starts one row in rather than after page 1.
        var offsetByOne = await NumbersAsync(ctx, "salesRepCustomerOrders(first:2, after:\"1\")");
        offsetByOne.Should().Equal("ORD-APRIL", "ORD-MARCH");
    }

    [Fact]
    public async Task TotalCount_IsTheWholeSetOnEveryPage()
    {
        using var ctx = await SeedThreeMonthsAsync();

        foreach (var after in new[] { "0", "2" })
        {
            var json = await ctx.ExecuteGraphQlAsync(
                $"query {{ salesRepCustomerOrders(first:2, after:\"{after}\") {{ totalCount items {{ number }} }} }}",
                userId: ctx.LastCreatedRepUserId);

            SalesRepTestContext.Node(json, "salesRepCustomerOrders").GetProperty("totalCount").GetInt32().Should().Be(3);
        }
    }

    [Fact]
    public async Task NonNumericCursor_ReadsAsTheFirstPage()
    {
        using var ctx = await SeedThreeMonthsAsync();

        // int.TryParse fails and Skip falls back to 0, so a caller that sends an opaque cursor silently
        // restarts the list instead of getting an error.
        var numbers = await NumbersAsync(ctx, "salesRepCustomerOrders(first:2, after:\"not-a-number\")");

        numbers.Should().Equal("ORD-MAY", "ORD-APRIL");
    }

    [Fact]
    public async Task CursorPastTheEnd_ReturnsNoRowsAndKeepsTheCount()
    {
        using var ctx = await SeedThreeMonthsAsync();

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(first:2, after:\"99\") { totalCount items { number } } }",
            userId: ctx.LastCreatedRepUserId);

        var node = SalesRepTestContext.Node(json, "salesRepCustomerOrders");
        node.GetProperty("items").EnumerateArray().Should().BeEmpty();
        node.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    private static async Task<SalesRepTestContext> SeedThreeMonthsAsync()
    {
        var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-march", org: "org-1", number: "ORD-MARCH", createdDate: _march, createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-april", org: "org-1", number: "ORD-APRIL", createdDate: _april, createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-may", org: "org-1", number: "ORD-MAY", createdDate: _may, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-march", "o-april", "o-may");

        return ctx;
    }

    private static async Task<IList<string>> NumbersAsync(SalesRepTestContext ctx, string field)
    {
        var json = await ctx.ExecuteGraphQlAsync($"query {{ {field} {{ items {{ number }} }} }}", userId: ctx.LastCreatedRepUserId);

        json.Should().NotContain("\"errors\"");

        return SalesRepTestContext.Node(json, "salesRepCustomerOrders")
            .GetProperty("items")
            .EnumerateArray()
            .Select(x => x.GetProperty("number").GetString())
            .ToList();
    }
}
