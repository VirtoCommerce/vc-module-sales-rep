using System;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The data-isolation invariant for orders: a rep reads the orders of the customers they serve, and nothing
/// else. Every order-returning surface of the Sales Rep endpoint is asserted, including the by-id read — an
/// order the rep must not see must stay unreadable even when its id is known.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepOrderIsolationGraphQlTests
{
    private static readonly DateTime _june = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string AllCustomerOrders = "query { salesRepCustomerOrders { totalCount items { number } } }";
    private const string RepOwnOrders = "query { salesRepOrders { totalCount items { number } } }";

    private static string CustomerOrders(string organizationId) =>
        $"query {{ salesRepCustomerOrders(organizationId:\"{organizationId}\") {{ totalCount items {{ number }} }} }}";

    private static string OrderById(string orderId) =>
        $"query {{ salesRepCustomerOrder(id:\"{orderId}\") {{ number }} }}";

    [Fact]
    public async Task OrderOfANeverServedCustomer_IsUnreadableOnEverySurface()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Someone else's order, for a customer this rep was never assigned to.
        OrderSeeder.Seed(ctx, id: "o-leak", org: "org-2", number: "ORD-LEAK", createdDate: _june, createdByUserId: "another-rep");
        await ctx.IndexOrdersAsync("o-leak");

        await AssertUnreadableAsync(ctx, rep.UserId, orderId: "o-leak", number: "ORD-LEAK", organizationId: "org-2");
    }

    [Fact]
    public async Task OrderTheRepPlacedForACustomerNoLongerServed_IsUnreadableOnEverySurface()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        // The rep's own order (CustomerId defaults to the rep), placed while they still served org-2.
        OrderSeeder.Seed(ctx, id: "o-mine", org: "org-2", number: "ORD-MINE", createdDate: _june);
        await ctx.IndexOrdersAsync("o-mine");

        // Precondition: while assigned, the rep reads it everywhere — otherwise the assertions below would
        // pass against an order that was never visible in the first place.
        (await ctx.ExecuteGraphQlAsync(AllCustomerOrders, rep.UserId)).Should().Contain("ORD-MINE");
        (await ctx.ExecuteGraphQlAsync(RepOwnOrders, rep.UserId)).Should().Contain("ORD-MINE");
        (await ctx.ExecuteGraphQlAsync(OrderById("o-mine"), rep.UserId)).Should().Contain("ORD-MINE");

        // Unassign org-2 through the real edit path, which revokes the membership the reads are scoped by.
        rep.Organizations = [new SalesRepOrganization { OrganizationId = "org-1" }];
        SalesRepTestContext.Unwrap(await ctx.Controller.Update(rep));

        // Having placed the order does not keep it readable here: the scope is who the rep serves now. The
        // index still holds the document — it is the organization scope that excludes it, not a re-index.
        await AssertUnreadableAsync(ctx, rep.UserId, orderId: "o-mine", number: "ORD-MINE", organizationId: "org-2");

        // Unassigning also drops the contact's own membership of that organization. That is what the
        // storefront's order queries authorize against, so the rep loses the organization-wide access there
        // too — though an order they placed themselves stays theirs to read as its buyer.
        var contact = await ctx.GetRequiredService<IMemberService>()
            .GetByIdAsync(rep.Id, nameof(MemberResponseGroup.Full)) as Contact;
        contact!.Organizations.Should().NotContain("org-2");
    }

    // The caller picks the customer, so an unserved id must narrow to nothing rather than being ignored —
    // silently falling back to "every served customer" would answer a question nobody asked.
    [Fact]
    public async Task AskingForAnUnservedCustomer_ReturnsNothingRatherThanTheServedOnes()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-served", org: "org-1", number: "ORD-SERVED", createdDate: _june, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-other", org: "org-2", number: "ORD-OTHER", createdDate: _june, organizationName: "Umbrella", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-served", "o-other");

        var json = await ctx.ExecuteGraphQlAsync(CustomerOrders("org-2"), rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-OTHER");
        json.Should().NotContain("ORD-SERVED");
    }

    // The search phrase reaches the index as-is, so it has to be unable to widen the scope: its clauses are
    // ANDed with the served-organization filter, which can only ever intersect to nothing.
    [Theory]
    [InlineData("organizationname:\\\"Umbrella\\\"")]
    [InlineData("organizationid:\\\"org-2\\\"")]
    public async Task AFilterNamingAnUnservedCustomer_CannotWidenTheScope(string filter)
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-served", org: "org-1", number: "ORD-SERVED", createdDate: _june, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-other", org: "org-2", number: "ORD-OTHER", createdDate: _june, organizationName: "Umbrella", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-served", "o-other");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerOrders(filter:\"{filter}\") {{ totalCount items {{ number }} }} }}",
            rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-OTHER");
        json.Should().NotContain("ORD-SERVED");
    }

    // Control for the case above: the same field names DO narrow when they name a served customer, so the
    // empty results there come from the scope filter, not from a phrase the backend quietly ignored.
    [Theory]
    [InlineData("organizationname:\\\"Acme\\\"")]
    [InlineData("organizationid:\\\"org-1\\\"")]
    public async Task AFilterNamingAServedCustomer_Narrows(string filter)
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        OrderSeeder.Seed(ctx, id: "o-served", org: "org-1", number: "ORD-SERVED", createdDate: _june, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-other", org: "org-2", number: "ORD-OTHER", createdDate: _june, organizationName: "Umbrella", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-served", "o-other");

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepCustomerOrders(filter:\"{filter}\") {{ totalCount items {{ number }} }} }}",
            rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-SERVED");
        json.Should().NotContain("ORD-OTHER");
    }

    private static async Task AssertUnreadableAsync(
        SalesRepTestContext ctx,
        string userId,
        string orderId,
        string number,
        string organizationId)
    {
        // The whole book of business.
        var allCustomers = await ctx.ExecuteGraphQlAsync(AllCustomerOrders, userId);
        allCustomers.Should().NotContain("\"errors\"");
        allCustomers.Should().Contain("\"totalCount\":0");
        allCustomers.Should().NotContain(number);

        // Asking for that customer by id does not widen the scope.
        var oneCustomer = await ctx.ExecuteGraphQlAsync(CustomerOrders(organizationId), userId);
        oneCustomer.Should().NotContain("\"errors\"");
        oneCustomer.Should().Contain("\"totalCount\":0");
        oneCustomer.Should().NotContain(number);

        // The dashboard's own list, scoped to the orders the rep placed.
        var ownOrders = await ctx.ExecuteGraphQlAsync(RepOwnOrders, userId);
        ownOrders.Should().NotContain("\"errors\"");
        ownOrders.Should().Contain("\"totalCount\":0");
        ownOrders.Should().NotContain(number);

        // And knowing the id is not enough either.
        var byId = await ctx.ExecuteGraphQlAsync(OrderById(orderId), userId);
        byId.Should().NotContain("\"errors\"");
        byId.Should().Contain("\"salesRepCustomerOrder\":null");
        byId.Should().NotContain(number);
    }
}
