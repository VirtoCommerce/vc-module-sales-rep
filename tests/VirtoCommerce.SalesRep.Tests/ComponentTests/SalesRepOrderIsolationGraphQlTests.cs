using System;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

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

    // The index decides which orders to load, the loaded rows decide which the rep may see. An order that left
    // the rep's book after it was indexed still matches the old organization's term filter, so without the
    // post-load check the stale document would serve it - with its current, freshly loaded contents.
    [Fact]
    public async Task OrderMovedOutOfTheRepsBookAfterIndexing_IsNotServedFromTheStaleDocument()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-MOVED", createdDate: _june, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        (await ctx.ExecuteGraphQlAsync(AllCustomerOrders, rep.UserId)).Should().Contain("ORD-MOVED");

        OrderSeeder.MoveToOrganization(ctx, "o-1", "org-2");

        var list = await ctx.ExecuteGraphQlAsync(AllCustomerOrders, rep.UserId);
        list.Should().NotContain("\"errors\"");
        list.Should().NotContain("ORD-MOVED");
        // The accepted trade: TotalCount is the index count, so it stays an upper bound until the reindex.
        list.Should().Contain("\"totalCount\":1");

        (await ctx.ExecuteGraphQlAsync(OrderById("o-1"), rep.UserId)).Should().NotContain("ORD-MOVED");
    }

    // Both order surfaces resolve the membership rule through the same access-service primitive, so a project
    // that overrides it changes what the list returns and what the by-id query answers together. Before they
    // shared one rule, the list scoped from a set the caller had resolved separately.
    [Fact]
    public async Task OverridingTheMembershipRule_ChangesBothOrderSurfaces()
    {
        using var ctx = SalesRepTestContext.Create(SalesRepAccessOverride.HidingOneOrganization);
        await ctx.SeedOrganizationsAsync("org-1", OrganizationAccessOverride.HiddenOrganizationId);
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", OrganizationAccessOverride.HiddenOrganizationId);

        OrderSeeder.Seed(ctx, id: "o-kept", org: "org-1", number: "ORD-KEPT", createdDate: _june, createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-hidden", org: OrganizationAccessOverride.HiddenOrganizationId, number: "ORD-HIDDEN", createdDate: _june, createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-kept", "o-hidden");

        var list = await ctx.ExecuteGraphQlAsync(AllCustomerOrders, rep.UserId);
        list.Should().NotContain("\"errors\"");
        list.Should().Contain("ORD-KEPT");
        list.Should().NotContain("ORD-HIDDEN");

        (await ctx.ExecuteGraphQlAsync(OrderById("o-kept"), rep.UserId)).Should().Contain("ORD-KEPT");
        (await ctx.ExecuteGraphQlAsync(OrderById("o-hidden"), rep.UserId)).Should().NotContain("ORD-HIDDEN");
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
