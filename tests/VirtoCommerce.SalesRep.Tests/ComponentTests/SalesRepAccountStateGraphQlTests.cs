using System;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The account behind a token can be locked, deleted or have its password expire while the token is still
/// valid - access tokens live 30 minutes. Claims alone cannot see any of that, and the module's membership
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

    private static readonly DateTime _june = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RepQuery)]
    [InlineData(SearchQuery)]
    [InlineData(Mutation)]
    public async Task LockedAccount_IsRefused(string request)
    {
        // Blocked through the module's own admin action, so the test locks an account the way production does.
        var json = await ExecuteAfterAsync(request, (ctx, rep) =>
            ctx.GetRequiredService<ISalesRepService>().BlockAsync(rep.Id));

        json.Should().Contain("\"errors\"");
        json.Should().Contain("locked");
    }

    [Theory]
    [InlineData(RepQuery)]
    [InlineData(SearchQuery)]
    [InlineData(Mutation)]
    public async Task DeletedAccount_IsRefused(string request)
    {
        // The account only - the member and its memberships stay, which is what leaves the token orphaned.
        var json = await ExecuteAfterAsync(request, (ctx, rep) => ctx.DeleteAccountAsync(rep.UserId));

        json.Should().Contain("\"errors\"");
    }

    [Theory]
    [InlineData(RepQuery)]
    [InlineData(SearchQuery)]
    [InlineData(Mutation)]
    public async Task ActiveAccount_IsServed(string request)
    {
        var json = await ExecuteAfterAsync(request, (_, _) => Task.CompletedTask);

        json.Should().NotContain("\"errors\"");
    }

    /// <summary>
    /// salesRepCustomerOrders is the one endpoint that overrides GetFieldType with a resolver of its own, so
    /// deriving from a gated base - which SalesRepEndpointGateTests asserts - does not by itself prove it is
    /// gated. This drives it, over the largest data surface the module exposes.
    /// </summary>
    [Fact]
    public async Task LockedAccount_CannotReadCustomerOrders()
    {
        const string request = "query { salesRepCustomerOrders { items { number } } }";

        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        OrderSeeder.Seed(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: _june, createdByUserId: "buyer-1");
        await ctx.IndexOrdersAsync("o-1");

        (await ctx.ExecuteGraphQlAsync(request, userId: rep.UserId)).Should().Contain("ORD-1");

        await ctx.GetRequiredService<ISalesRepService>().BlockAsync(rep.Id);

        var json = await ctx.ExecuteGraphQlAsync(request, userId: rep.UserId);
        json.Should().Contain("\"errors\"");
        json.Should().NotContain("ORD-1");
    }

    private static async Task<string> ExecuteAfterAsync(string request, Func<SalesRepTestContext, SalesRepDetails, Task> change)
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        await change(ctx, rep);

        return await ctx.ExecuteGraphQlAsync(request, userId: rep.UserId);
    }
}
