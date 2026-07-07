using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
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
    [Fact]
    public async Task CustomerSalesReps_ReturnsRepsServingCallerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // A rep serving only org-2 must NOT appear for an org-1 member.
        await ctx.CreateRepAsync("Other", "Rep", "other@test.com", "org-2");

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
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

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
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

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
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "Acme", "Globex", "Initech", "Umbrella");

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
        var alice = await ctx.CreateRepAsync("Alice", "Anderson", "alice@test.com", "AcmeOrg");
        var bob = await ctx.CreateRepAsync("Bob", "Brown", "bob@test.com", "AcmeOrg");
        var carol = await ctx.CreateRepAsync("Carol", "Clark", "carol@test.com", "AcmeOrg");

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
        await ctx.CreateRepAsync("Active", "Rep", "active@test.com", "AcmeOrg");
        var blocked = await ctx.CreateRepAsync("Blocked", "Rep", "blocked@test.com", "AcmeOrg");

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
        await ctx.CreateRepAsync("Active", "Rep", "active@test.com", "AcmeOrg");
        var locked = await ctx.CreateRepAsync("Locked", "Rep", "locked@test.com", "AcmeOrg");

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
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "OrgKeep", "OrgLocked");

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

    // ---- salesRepCustomer(id) — single customer details (VCST-5308) ----

    [Fact]
    public async Task SalesRepCustomer_ReturnsDetailsForServedOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-1\") { organizationId organizationName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"organizationId\":\"org-1\"");
        json.Should().Contain("\"organizationName\":\"org-1\"");
    }

    [Fact]
    public async Task SalesRepCustomer_ForOrganizationNotServed_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // The rep serves org-1 only; requesting org-2 (which exists) must not leak it — a rep cannot read an
        // arbitrary organization by guessing its id.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-2\") { organizationId organizationName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomer\":null");
    }

    [Fact]
    public async Task SalesRepCustomer_WhenMembershipLocked_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // A rep locked in the organization must not see it as a customer (mirrors the list-query lock filter).
        var membershipId = rep.Organizations.Single(o => o.OrganizationId == "org-1").MembershipId;
        await ctx.GetRequiredService<VirtoCommerce.CustomerModule.Core.Services.IOrganizationMembershipService>().LockAsync(membershipId);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-1\") { organizationId } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomer\":null");
    }

    [Fact]
    public async Task SalesRepCustomer_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCustomer(id:\"org-1\") { organizationId } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepCustomer_ResolvesOwnerAsPrimaryContact()
    {
        using var ctx = SalesRepTestContext.Create();
        // Owner contact (with its own phone); the organization points at it via OwnerId.
        await ctx.SeedContactAsync("owner-1", c =>
        {
            c.FirstName = "Olivia";
            c.LastName = "Owner";
            c.FullName = "Olivia Owner";
            c.Name = "Olivia Owner";
            c.Phones = ["+1-999-0000"];
        });
        await ctx.SeedOrganizationAsync("org-1", o => o.OwnerId = "owner-1");
        // The rep also becomes a contact member of org-1, but the explicit owner must win over the fallback.
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-1\") { primaryContact { id fullName phones } phone } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("owner-1").And.Contain("Olivia Owner");
        json.Should().Contain("999-0000");       // phone taken from the primary contact (the "+" is JSON-escaped)
        json.Should().NotContain("Jane Rep");    // the rep is a member but not the primary contact
    }

    [Fact]
    public async Task SalesRepCustomer_FallsBackToFirstContactWhenNoOwner()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1"); // no owner set
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // With no owner, the primary contact falls back to the org's first contact member — here the rep itself.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-1\") { primaryContact { id fullName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain(rep.Id).And.Contain("Jane Rep");
    }

    [Fact]
    public async Task SalesRepCustomer_MapsAccountTypeAndShipTo()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1", o =>
        {
            o.BusinessCategory = "Retailer";
            // CountryName + RegionName are set so MemberService.FillAddressNames doesn't call the (dataless)
            // CountriesService in the harness; shipTo formats from City + RegionName.
            o.Addresses = [new VirtoCommerce.CustomerModule.Core.Model.Address { Line1 = "1 Main St", City = "Seattle", RegionName = "WA", CountryName = "United States", CountryCode = "US", PostalCode = "98101", IsDefault = true }];
        });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(id:\"org-1\") { accountType shipTo } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"accountType\":\"Retailer\"");
        json.Should().Contain("\"shipTo\":\"Seattle, WA\"");
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
