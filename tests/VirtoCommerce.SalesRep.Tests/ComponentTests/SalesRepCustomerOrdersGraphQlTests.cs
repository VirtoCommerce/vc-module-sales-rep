using System;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// salesRepCustomerOrders / salesRepCustomerOrder (VCST-5733): every order of a served customer, not only the ones
/// the rep placed. Orders are seeded into SQLite, indexed into the in-memory Lucene index with the real order
/// document builder, and read back through the real scoped schema — the same indexed path production uses.
/// </summary>
public class SalesRepCustomerOrdersGraphQlTests
{
    private static readonly DateTime _june = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _may = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CustomerOrders_ReturnOrdersPlacedByAnyone()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-mine", org: "org-1", number: "ORD-MINE", createdDate: _june);
        OrderSeeder.Seed(ctx, id: "o-theirs", org: "org-1", number: "ORD-THEIRS", createdDate: _may, createdByUserId: "another-rep");
        await ctx.IndexOrdersAsync("o-mine", "o-theirs");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-MINE");
        json.Should().Contain("ORD-THEIRS");
    }

    [Fact]
    public async Task SalesRepOrders_StillOnlyReturnsOrdersThisRepPlaced()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-mine", org: "org-1", number: "ORD-MINE", createdDate: _june);
        OrderSeeder.Seed(ctx, id: "o-theirs", org: "org-1", number: "ORD-THEIRS", createdDate: _may, createdByUserId: "another-rep");

        // The dashboard widget and the statistics keep the creator scoping the new page drops.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-MINE");
        json.Should().NotContain("ORD-THEIRS");
    }

    [Fact]
    public async Task CustomerOrders_ForOrganizationNotServed_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-leak", org: "org-2", number: "ORD-LEAK", createdDate: _june, createdByUserId: "another-rep");
        await ctx.IndexOrdersAsync("o-leak");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(organizationId:\"org-2\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-LEAK");
    }

    [Fact]
    public async Task CustomerOrders_WithoutOrganizationId_CoverServedCustomersOnly()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-ONE", createdDate: _june, createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-2", org: "org-2", number: "ORD-TWO", createdDate: _may, createdByUserId: "buyer-2");
        OrderSeeder.Seed(ctx, id: "o-3", org: "org-3", number: "ORD-LEAK", createdDate: _june, createdByUserId: "buyer-3");
        await ctx.IndexOrdersAsync("o-1", "o-2", "o-3");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-ONE");
        json.Should().Contain("ORD-TWO");
        json.Should().NotContain("ORD-LEAK");
    }

    [Fact]
    public async Task CustomerOrders_StatusFacet_CountsEveryStatusInScope()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, status: "New", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-2", org: "org-1", number: "ORD-2", createdDate: _may, status: "Completed", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-3", org: "org-1", number: "ORD-3", createdDate: _may, status: "Completed", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-1", "o-2", "o-3");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(organizationId:\"org-1\", facet:\"status\") " +
            "{ totalCount term_facets { name terms { term count } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":3");
        json.Should().Contain("\"term\":\"Completed\",\"count\":2");
        json.Should().Contain("\"term\":\"New\",\"count\":1");
    }

    [Fact]
    public async Task CustomerOrders_FilterAcceptsSeveralStatuses()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: _june, status: "New", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-done", org: "org-1", number: "ORD-DONE", createdDate: _may, status: "Completed", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: _may, status: "Cancelled", createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-new", "o-done", "o-cancelled");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(organizationId:\"org-1\", filter:\"status:\\\"New\\\",\\\"Completed\\\"\") " +
            "{ totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-NEW");
        json.Should().Contain("ORD-DONE");
        json.Should().NotContain("ORD-CANCELLED");
    }

    [Fact]
    public async Task CustomerOrder_ById_ReturnsAnOrderPlacedByAnyoneOfAServedCustomer()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, itemsCount: 2, createdByUserId: "another-rep");

        // The detail read goes straight to the order, so it needs no index.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrder(id:\"o-1\") { number status items { sku } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-1");
        json.Should().Contain("SKU-0");
    }

    [Fact]
    public async Task CustomerOrder_ById_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-leak", org: "org-2", number: "ORD-LEAK", createdDate: _june, createdByUserId: "another-rep");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrder(id:\"o-leak\") { number } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerOrder\":null");
        json.Should().NotContain("ORD-LEAK");
    }
}
