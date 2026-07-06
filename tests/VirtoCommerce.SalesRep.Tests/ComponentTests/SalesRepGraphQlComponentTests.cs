using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// End-to-end component tests for the Sales Rep X-API: seed via the real <c>SalesRepController</c>, execute
/// real GraphQL query strings through the real scoped schema (builders + MediatR handlers + services over
/// in-memory SQLite), and assert on the GraphQL response. No mocks.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepGraphQlComponentTests
{
    private static SalesRepDetails SimpleRep(string firstName, string lastName, string email, params string[] orgIds) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Emails = [email],
        Phones = ["+1-555-0100"],
        Password = "P@ssw0rd123!",
        Organizations = orgIds.Select(id => new SalesRepOrganization { OrganizationId = id }).ToList(),
    };

    [Fact]
    public async Task CustomerSalesReps_ReturnsRepsServingCallerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1")));
        // A rep serving only org-2 must NOT appear for an org-1 member.
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Other", "Rep", "other@test.com", "org-2")));

        // Caller is a member of org-1 (organization_id claim).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { totalCount items { id fullName emails phones } } }",
            userId: "any-member",
            organizationId: "org-1");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("jane@test.com");
        json.Should().Contain("Jane Rep");
        json.Should().NotContain("other@test.com");
        json.Should().Contain(rep.Id);
    }

    [Fact]
    public async Task CustomerSalesReps_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { customerSalesReps { totalCount items { id } } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepCustomers_ReturnsOrganizationsServedByCaller()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1", "org-2")));

        // Caller is the rep (their security-account id).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { totalCount items { organizationId organizationName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("org-1");
        json.Should().Contain("org-2");
        json.Should().NotContain("org-3"); // the rep does not serve org-3
    }

    [Fact]
    public async Task SalesRepCustomers_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCustomers { totalCount items { organizationId } } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepCustomers_WithLastOrder_ReturnsMostRecentOrderPerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1")));

        SeedOrder(ctx, id: "o-old", org: "org-1", number: "ORD-OLD", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId lastOrder { number total currency } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-NEW");   // most recent
        json.Should().NotContain("ORD-OLD"); // older order is not the "last order"
        json.Should().Contain("123.45");    // Total must be hydrated, not 0
        json.Should().Contain("USD");
    }

    [Fact]
    public async Task SalesRepCustomers_SupportsPagingKeywordAndSort()
    {
        using var ctx = SalesRepTestContext.Create();
        // Distinct, no-common-substring names so keyword/sort are unambiguous.
        await ctx.SeedOrganizationsAsync("Acme", "Globex", "Initech", "Umbrella");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(
            SimpleRep("Jane", "Rep", "jane@test.com", "Acme", "Globex", "Initech", "Umbrella")));

        // Page 1 (name asc): Acme, Globex
        var page1 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:2, after:\"0\", sort:\"name:asc\") { totalCount pageInfo{ hasNextPage endCursor } items{ organizationName } } }",
            userId: rep.UserId);
        page1.Should().Contain("\"totalCount\":4").And.Contain("\"hasNextPage\":true");
        page1.Should().Contain("Acme").And.Contain("Globex");
        page1.Should().NotContain("Initech").And.NotContain("Umbrella");

        // Page 2 (after:2): Initech, Umbrella — no overlap with page 1
        var page2 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:2, after:\"2\", sort:\"name:asc\") { totalCount pageInfo{ hasNextPage } items{ organizationName } } }",
            userId: rep.UserId);
        page2.Should().Contain("Initech").And.Contain("Umbrella");
        page2.Should().NotContain("Acme").And.NotContain("Globex");
        page2.Should().Contain("\"hasNextPage\":false");

        // Keyword filtering routes to the member search index — populate it for the org members first.
        await ctx.IndexMembersAsync("Acme", "Globex", "Initech", "Umbrella");
        var keyword = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(keyword:\"Globex\") { totalCount items{ organizationName } } }",
            userId: rep.UserId);
        keyword.Should().Contain("\"totalCount\":1").And.Contain("Globex");
        keyword.Should().NotContain("Acme");

        // Sort desc: first item must be Umbrella (last alphabetically)
        var desc = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:1, sort:\"name:desc\") { items{ organizationName } } }",
            userId: rep.UserId);
        desc.Should().Contain("Umbrella").And.NotContain("Acme");
    }

    [Fact]
    public async Task CustomerSalesReps_SupportsPagingKeywordAndSort()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("AcmeOrg");
        var alice = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Alice", "Anderson", "alice@test.com", "AcmeOrg")));
        var bob = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Bob", "Brown", "bob@test.com", "AcmeOrg")));
        var carol = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Carol", "Clark", "carol@test.com", "AcmeOrg")));

        // Page 1 (name asc): Alice Anderson, Bob Brown
        var page1 = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(first:2, after:\"0\", sort:\"name:asc\") { totalCount pageInfo{ hasNextPage } items{ fullName } } }",
            organizationId: "AcmeOrg");
        page1.Should().Contain("\"totalCount\":3").And.Contain("\"hasNextPage\":true");
        page1.Should().Contain("Alice Anderson").And.Contain("Bob Brown");
        page1.Should().NotContain("Carol Clark");

        // Page 2 (after:2): Carol Clark — last page
        var page2 = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(first:2, after:\"2\", sort:\"name:asc\") { pageInfo{ hasNextPage } items{ fullName } } }",
            organizationId: "AcmeOrg");
        page2.Should().Contain("Carol Clark").And.Contain("\"hasNextPage\":false");
        page2.Should().NotContain("Alice Anderson");

        // Keyword filtering routes to the member search index — populate it for the rep contacts first.
        await ctx.IndexMembersAsync(alice.Id, bob.Id, carol.Id);
        var keyword = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(keyword:\"Brown\") { totalCount items{ fullName } } }",
            organizationId: "AcmeOrg");
        keyword.Should().Contain("\"totalCount\":1").And.Contain("Bob Brown");
        keyword.Should().NotContain("Alice Anderson");

        // Sort desc: first item must be Carol Clark
        var desc = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(first:1, sort:\"name:desc\") { items{ fullName } } }",
            organizationId: "AcmeOrg");
        desc.Should().Contain("Carol Clark").And.NotContain("Alice Anderson");
    }

    [Fact]
    public async Task CustomerSalesReps_ExcludesBlockedAccounts()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("AcmeOrg");
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Active", "Rep", "active@test.com", "AcmeOrg")));
        var blocked = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Blocked", "Rep", "blocked@test.com", "AcmeOrg")));

        // Block one rep's account (sets LockoutEnd, exactly like the admin "Block" action).
        await ctx.GetRequiredService<VirtoCommerce.SalesRep.Core.Services.ISalesRepService>().BlockAsync(blocked.Id);

        // VCST-4907 #5: only active accounts (not blocked/disabled/deleted) are returned.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { totalCount items{ fullName } } }",
            organizationId: "AcmeOrg");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("Active Rep");
        json.Should().NotContain("Blocked Rep");
    }

    [Fact]
    public async Task CustomerSalesReps_ExcludesPerOrgLockedReps()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("AcmeOrg");
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Active", "Rep", "active@test.com", "AcmeOrg")));
        var locked = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Locked", "Rep", "locked@test.com", "AcmeOrg")));

        // Lock the second rep's membership in AcmeOrg (a per-org lock, not an account-level block).
        var membershipId = locked.Organizations.Single(o => o.OrganizationId == "AcmeOrg").MembershipId;
        await ctx.GetRequiredService<VirtoCommerce.CustomerModule.Core.Services.IOrganizationMembershipService>().LockAsync(membershipId);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { totalCount items{ fullName } } }",
            organizationId: "AcmeOrg");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("Active Rep");
        json.Should().NotContain("Locked Rep");
    }

    [Fact]
    public async Task SalesRepCustomers_ExcludesOrganizationsWhereRepIsLocked()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("OrgKeep", "OrgLocked");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(
            SimpleRep("Jane", "Rep", "jane@test.com", "OrgKeep", "OrgLocked")));

        // Lock the rep's membership in OrgLocked only.
        var lockedMembershipId = rep.Organizations.Single(o => o.OrganizationId == "OrgLocked").MembershipId;
        await ctx.GetRequiredService<VirtoCommerce.CustomerModule.Core.Services.IOrganizationMembershipService>().LockAsync(lockedMembershipId);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { totalCount items{ organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("OrgKeep");
        json.Should().NotContain("OrgLocked");
    }

    private static void SeedOrder(SalesRepTestContext ctx, string id, string org, string number, DateTime createdDate)
    {
        using var db = ctx.NewOrderDbContext();
        db.Add(new CustomerOrderEntity
        {
            Id = id,
            Number = number,
            OrganizationId = org,
            CustomerId = "customer-1",
            CustomerName = "Customer 1",
            StoreId = "B2B-store",
            Status = "New",
            Currency = "USD",
            Total = 123.45m,
            IsPrototype = false,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        });
        db.SaveChanges();
    }
}
