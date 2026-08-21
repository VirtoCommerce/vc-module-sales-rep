using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// salesRepCustomerOrders / salesRepCustomerOrder (VCST-5733): every order of a served customer, not only the ones
/// the rep placed. Orders are seeded into SQLite, indexed into the in-memory Lucene index with the real order
/// document builder, and read back through the real scoped schema — the same indexed path production uses.
/// </summary>
[Trait("Category", "Component")]
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

    // The all-customers page facets on the owning organization as well, to offer a customer filter.
    [Fact]
    public async Task CustomerOrders_CustomerFacet_CountsOrdersPerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-2", org: "org-1", number: "ORD-2", createdDate: _may, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-3", org: "org-2", number: "ORD-3", createdDate: _may, organizationName: "Umbrella", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-1", "o-2", "o-3");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(facet:\"status organizationname\") " +
            "{ totalCount term_facets { name terms { term count } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"organizationname\"");
        json.Should().Contain("\"term\":\"Acme\",\"count\":2");
        json.Should().Contain("\"term\":\"Umbrella\",\"count\":1");
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

    // Aggregations are not scoped by the search filter, so a facet naming a scoping field would count across
    // the whole index. Only the fields the module offers may be aggregated.
    [Fact]
    public async Task CustomerOrders_FacetOnAScopingField_IsRefused()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-served", org: "org-1", number: "ORD-SERVED", createdDate: _june, organizationName: "Acme", createdByUserId: "buyer-1");
        OrderSeeder.Seed(ctx, id: "o-other", org: "org-2", number: "ORD-OTHER", createdDate: _june, organizationName: "Umbrella", createdByUserId: "buyer-2");
        await ctx.IndexOrdersAsync("o-served", "o-other");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(facet:\"organizationid storeid status\") " +
            "{ term_facets { name terms { term count } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // The scoping fields are dropped; the one legitimate facet still answers.
        json.Should().NotContain("\"name\":\"organizationid\"");
        json.Should().NotContain("\"name\":\"storeid\"");
        json.Should().NotContain("org-2");
        json.Should().Contain("\"name\":\"status\"");
    }

    [Fact]
    public async Task CustomerOrders_FacetName_ComesBackInTheModuleSpelling()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        // The field name is echoed back as the facet name, and the providers match it case-insensitively, so the
        // caller's casing must not decide what the same facet is called from one request to the next.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders(facet:\"STATUS\") { term_facets { name } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"status\"");
        json.Should().NotContain("\"name\":\"STATUS\"");
    }

    // The localized fields of CustomerOrderType read the culture from the user context, not from an argument
    // of their own, so the query's cultureName has to reach them.
    [Fact]
    public async Task CustomerOrder_ById_LocalizesTheStatusToTheRequestedCulture()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, status: "New", createdByUserId: "buyer-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrder(id:\"o-1\", cultureName:\"en-US\") { number status statusDisplayValue } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"status\":\"New\"");
        // The harness's stub renders a status as "<raw> (<culture>)", so a culture that never reached the
        // resolver would come back as the bare "New".
        json.Should().Contain("\"statusDisplayValue\":\"New (en-US)\"");
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

    // The response group the selection produces is asserted where production computes it — over the real schema —
    // because the raw field paths arrive wrapped in the connection ("items.total.formattedAmount"), and reading
    // that wrapper as the order's own line items would put every list back on the full graph.
    [Fact]
    public async Task CustomerOrders_ListSelection_LoadsTheOrderRowOnly()
    {
        using var ctx = SalesRepTestContext.Create(IndexedOrderSearchOverride.Recording);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, itemsCount: 2, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { totalCount items { number status statusDisplayValue createdDate " +
            "organizationName total { formattedAmount } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-1");
        RecordedResponseGroup(ctx).Should().Be(CustomerOrderResponseGroup.WithPrices);
    }

    [Fact]
    public async Task CustomerOrders_SelectingLineItems_LoadsTheFullGraph()
    {
        using var ctx = SalesRepTestContext.Create(IndexedOrderSearchOverride.Recording);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, itemsCount: 2, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { items { number items { sku } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("SKU-0");
        RecordedResponseGroup(ctx).Should().Be(CustomerOrderResponseGroup.Full);
    }

    // A narrowed load without WithPrices comes back with the money zeroed, so the two lists showing the same order
    // would disagree about its total. (The harness reads orders straight from the repository, so this covers the
    // repository's own price reset, not the totals recalculation CustomerOrderService layers on top.)
    [Fact]
    public async Task CustomerOrders_ReportTheSameTotalAsMyRecentOrders()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, itemsCount: 2, total: 123.45m);
        await ctx.IndexOrdersAsync("o-1");

        var mine = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders { items { number total { amount } } } }",
            userId: rep.UserId);
        var all = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { items { number total { amount } } } }",
            userId: rep.UserId);

        mine.Should().NotContain("\"errors\"");
        all.Should().NotContain("\"errors\"");
        mine.Should().Contain("\"amount\":123.45");
        all.Should().Contain("\"amount\":123.45");
    }

    private static CustomerOrderResponseGroup RecordedResponseGroup(SalesRepTestContext ctx)
    {
        var responseGroup = ctx.GetRequiredService<RecordingIndexedCustomerOrderSearchService>().ResponseGroups.Single();

        return EnumUtility.SafeParseFlags(responseGroup, CustomerOrderResponseGroup.Full);
    }
}
