using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The account behind a token can be locked, deleted or have its password expire while the token is still
/// valid — access tokens live 30 minutes. Claims alone cannot see any of that, and the module's membership
/// scoping does not either: OrganizationMembership.IsLocked is the membership, not the account. So every entry
/// point re-checks the account, and these tests hold each of the three roots to it.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepAccountStateGraphQlTests
{
    private const string RepQuery = "query { salesRepCustomer(organizationId:\"org-1\") { organizationId } }";
    private const string SearchQuery = "query { salesRepCustomers { totalCount items { organizationId } } }";
    private const string Mutation =
        "mutation { saveSalesRepLayout(command: { scope: \"dashboard\", storeId: \"B2B-store\", schemaVersion: 1, regions: [] }) { schemaVersion } }";

    [Theory]
    [InlineData(RepQuery)]
    [InlineData(SearchQuery)]
    [InlineData(Mutation)]
    public async Task LockedAccount_IsRefused(string request)
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Still serving org-1 — the membership is untouched, only the login account is locked.
        await ctx.LockAccountAsync(rep.UserId);

        var json = await ctx.ExecuteGraphQlAsync(request, userId: rep.UserId);

        json.Should().Contain("\"errors\"");
        json.Should().Contain("locked");
    }

    [Theory]
    [InlineData(RepQuery)]
    [InlineData(SearchQuery)]
    [InlineData(Mutation)]
    public async Task DeletedAccount_IsRefused(string request)
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await ctx.DeleteAccountAsync(rep.UserId);

        var json = await ctx.ExecuteGraphQlAsync(request, userId: rep.UserId);

        json.Should().Contain("\"errors\"");
    }

    [Fact]
    public async Task LockedAccount_CannotReadCustomerOrders()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1",
            createdDate: new System.DateTime(2026, 6, 1, 0, 0, 0, System.DateTimeKind.Utc),
            createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        // The largest data surface the module exposes: the whole customer order graph.
        var before = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { items { number } } }", userId: rep.UserId);
        before.Should().Contain("ORD-1");

        await ctx.LockAccountAsync(rep.UserId);

        var after = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrders { items { number } } }", userId: rep.UserId);
        after.Should().Contain("\"errors\"");
        after.Should().NotContain("ORD-1");
    }

    [Fact]
    public async Task ActiveAccount_IsStillServed()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(RepQuery, userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("org-1");
    }
}
