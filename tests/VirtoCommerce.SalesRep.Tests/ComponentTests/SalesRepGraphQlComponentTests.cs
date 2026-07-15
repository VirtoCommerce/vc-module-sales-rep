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
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), itemsCount: 2);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId lastOrder { number total { amount formattedAmount currency { code } } itemsCount } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-NEW");   // most recent
        json.Should().NotContain("ORD-OLD"); // older order is not the "last order"
        json.Should().Contain("123.45");            // total.amount hydrated, not 0
        json.Should().Contain("$123.45");           // total.formattedAmount (invariant culture on the lastOrder path)
        json.Should().Contain("\"code\":\"USD\"");  // total.currency resolved from the order's currency code
        json.Should().Contain("\"itemsCount\":2"); // line items hydrated on the lastOrder path too, not 0
    }

    [Fact]
    public async Task SalesRepCustomers_LastOrder_LocalizesWithCultureName()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        // The cultureName argument on salesRepCustomers must reach the nested lastOrder SalesRepOrderType resolvers
        // (the builder copies it to the UserContext). Asserted on both culture-dependent fields: statusDisplayValue
        // (StubLocalizableSettingService renders "<raw> (<culture>)") and total.formattedAmount.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(cultureName:\"en-US\") { items { lastOrder { status statusDisplayValue total { formattedAmount } } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"status\":\"Cancelled\"");
        json.Should().Contain("\"statusDisplayValue\":\"Cancelled (en-US)\"");
        json.Should().Contain("\"formattedAmount\":\"$123.45\""); // money localized on the lastOrder path too
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
            "query { salesRepCustomer(organizationId:\"org-1\") { organizationId organizationName } }",
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
            "query { salesRepCustomer(organizationId:\"org-2\") { organizationId organizationName } }",
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
            "query { salesRepCustomer(organizationId:\"org-1\") { organizationId } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomer\":null");
    }

    [Fact]
    public async Task SalesRepCustomer_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { organizationId } }");

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
            "query { salesRepCustomer(organizationId:\"org-1\") { primaryContact { id fullName phones } phone } }",
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
            "query { salesRepCustomer(organizationId:\"org-1\") { primaryContact { id fullName } } }",
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
            "query { salesRepCustomer(organizationId:\"org-1\") { accountType shipTo } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"accountType\":\"Retailer\"");
        json.Should().Contain("\"shipTo\":\"Seattle, WA\"");
    }

    [Fact]
    public async Task CustomerSalesReps_AreScopedByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("AcmeOrg");
        // Two reps serve the SAME org, but their accounts belong to DIFFERENT stores. A rep's account is
        // store-bound, so scoping must include one store's rep and exclude the other's.
        await ctx.CreateRepInStoreAsync("Bea", "B2B", "bea@test.com", "B2B-store", "AcmeOrg");
        await ctx.CreateRepInStoreAsync("Otto", "Other", "otto@test.com", "OtherStore", "AcmeOrg");

        // Scoped to B2B-store: only the B2B rep.
        var b2b = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(storeId:\"B2B-store\") { totalCount items { fullName } } }",
            organizationId: "AcmeOrg");
        b2b.Should().NotContain("\"errors\"");
        b2b.Should().Contain("\"totalCount\":1").And.Contain("Bea B2B");
        b2b.Should().NotContain("Otto Other");

        // Scoped to the other store: only that store's rep — proves the filter keys on the value, not a fixed side.
        var other = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps(storeId:\"OtherStore\") { totalCount items { fullName } } }",
            organizationId: "AcmeOrg");
        other.Should().Contain("\"totalCount\":1").And.Contain("Otto Other");
        other.Should().NotContain("Bea B2B");

        // No store filter: both reps (confirms the data really spans stores and that null = all stores).
        var all = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { totalCount items { fullName } } }",
            organizationId: "AcmeOrg");
        all.Should().Contain("\"totalCount\":2").And.Contain("Bea B2B").And.Contain("Otto Other");
    }

    [Fact]
    public async Task SalesRepCustomers_LastOrder_IsScopedByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // A NEWER order in another store must be ignored when the query is scoped to B2B-store — otherwise a rep
        // could see order metadata from a store outside the current storefront.
        // org-1 has orders in two stores; the NEWER order is in OtherStore.
        SeedOrder(ctx, id: "o-other", org: "org-1", number: "ORD-OTHER", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), storeId: "OtherStore");
        SeedOrder(ctx, id: "o-b2b", org: "org-1", number: "ORD-B2B", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), storeId: "B2B-store");

        // Scoped to B2B-store: the B2B order, even though the OtherStore order is more recent.
        var b2b = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(storeId:\"B2B-store\") { items { organizationId lastOrder { number } } } }",
            userId: rep.UserId);
        b2b.Should().NotContain("\"errors\"");
        b2b.Should().Contain("ORD-B2B").And.NotContain("ORD-OTHER");

        // Scoped to the other store: that store's order — proves the filter keys on the value, not a fixed side.
        var other = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(storeId:\"OtherStore\") { items { lastOrder { number } } } }",
            userId: rep.UserId);
        other.Should().Contain("ORD-OTHER").And.NotContain("ORD-B2B");

        // No store filter: the globally most-recent order across stores (ORD-OTHER).
        var all = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { lastOrder { number } } } }",
            userId: rep.UserId);
        all.Should().Contain("ORD-OTHER").And.NotContain("ORD-B2B");
    }

    // ---- salesRepOrders(organizationId) — a customer's orders (VCST-5308) ----

    [Fact]
    public async Task SalesRepOrders_ReturnsCustomerOrders_RecentFirst()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        SeedOrder(ctx, id: "o-old", org: "org-1", number: "ORD-OLD", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number createdDate status total { amount formattedAmount currency { code } } itemsCount } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-OLD").And.Contain("ORD-NEW");
        json.Should().Contain("123.45");            // total.amount hydrated, not 0
        json.Should().Contain("$123.45");           // total.formattedAmount
        json.Should().Contain("\"code\":\"USD\"");  // total.currency resolved from the order's currency code
        // Default sort is createdDate:desc — the newest order must appear before the older one.
        json.IndexOf("ORD-NEW", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("ORD-OLD", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepOrders_ReturnsItemsCount()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), itemsCount: 3);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { items { number itemsCount } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"itemsCount\":3");
    }

    [Fact]
    public async Task SalesRepOrders_ForOrganizationNotServed_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // An order exists for org-2, but the rep does not serve org-2 and must not read it by guessing the id.
        SeedOrder(ctx, id: "o-2", org: "org-2", number: "ORD-LEAK", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-2\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-LEAK");
    }

    [Fact]
    public async Task SalesRepOrders_WhenMembershipLocked_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // A rep locked in the organization must not see its orders (mirrors the customer-details lock filter).
        var membershipId = rep.Organizations.Single(o => o.OrganizationId == "org-1").MembershipId;
        await ctx.GetRequiredService<VirtoCommerce.CustomerModule.Core.Services.IOrganizationMembershipService>().LockAsync(membershipId);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-1");
    }

    [Fact]
    public async Task SalesRepOrders_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepOrders_SupportsPaging()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Three orders, newest first: ORD-3 (Jun) > ORD-2 (May) > ORD-1 (Apr).
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-2", org: "org-1", number: "ORD-2", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-3", org: "org-1", number: "ORD-3", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Page 1 (first:2): the two most recent orders.
        var page1 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", first:2, after:\"0\") { totalCount pageInfo{ hasNextPage } items{ number } } }",
            userId: rep.UserId);
        page1.Should().Contain("\"totalCount\":3").And.Contain("\"hasNextPage\":true");
        page1.Should().Contain("ORD-3").And.Contain("ORD-2").And.NotContain("ORD-1");

        // Page 2 (after:2): the remaining order, no overlap with page 1.
        var page2 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", first:2, after:\"2\") { pageInfo{ hasNextPage } items{ number } } }",
            userId: rep.UserId);
        page2.Should().Contain("ORD-1").And.NotContain("ORD-3").And.NotContain("ORD-2");
        page2.Should().Contain("\"hasNextPage\":false");
    }

    [Fact]
    public async Task SalesRepOrders_AreScopedByStore()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-b2b", org: "org-1", number: "ORD-B2B", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), storeId: "B2B-store");
        SeedOrder(ctx, id: "o-other", org: "org-1", number: "ORD-OTHER", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), storeId: "OtherStore");

        // Scoped to B2B-store: only the B2B order, even though the OtherStore order is more recent.
        var b2b = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", storeId:\"B2B-store\") { totalCount items { number } } }",
            userId: rep.UserId);
        b2b.Should().NotContain("\"errors\"");
        b2b.Should().Contain("\"totalCount\":1").And.Contain("ORD-B2B").And.NotContain("ORD-OTHER");

        // No store filter: both orders across stores.
        var all = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);
        all.Should().Contain("\"totalCount\":2").And.Contain("ORD-B2B").And.Contain("ORD-OTHER");
    }

    [Fact]
    public async Task SalesRepOrders_FiltersByKeyword()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-alpha", org: "org-1", number: "ORD-ALPHA", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-beta", org: "org-1", number: "ORD-BETA", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Keyword matches the order number (Orders CustomerOrderSearchService: Number/CustomerName Contains).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", keyword:\"ALPHA\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-ALPHA");
        json.Should().NotContain("ORD-BETA");
    }

    // ---- order statuses + status filter (VCST-5308) ----

    [Fact]
    public async Task SalesRepOrderStatuses_ReturnsStatuses()
    {
        using var ctx = SalesRepTestContext.Create();

        // Caller-agnostic (statuses are store config), but the scoped schema requires authentication.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderStatuses(storeId:\"B2B-store\") { name localizedName } }",
            userId: "any-authenticated-user");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"New\"");
        json.Should().Contain("\"name\":\"Inactive\"").And.Contain("Not active"); // composite status, localized label
    }

    [Fact]
    public async Task SalesRepOrderStatuses_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepOrderStatuses(storeId:\"B2B-store\") { name } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepOrders_FiltersBySelectedStatus_ResolvesComposite()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");
        SeedOrder(ctx, id: "o-failed", org: "org-1", number: "ORD-FAILED", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Failed");

        // "Inactive" is a composite status -> [Cancelled, Failed] (StubOrderStatusService); "New" must be excluded.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", statuses:[\"Inactive\"]) { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-CANCELLED").And.Contain("ORD-FAILED");
        json.Should().NotContain("ORD-NEW");
    }

    [Fact]
    public async Task SalesRepOrders_FiltersByMultipleStatuses_Union()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");
        SeedOrder(ctx, id: "o-processing", org: "org-1", number: "ORD-PROCESSING", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Processing");

        // Multi-select: "New" (-> [New]) + "Inactive" (-> [Cancelled, Failed]); union excludes Processing.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", statuses:[\"New\",\"Inactive\"]) { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-NEW").And.Contain("ORD-CANCELLED");
        json.Should().NotContain("ORD-PROCESSING");
    }

    [Fact]
    public async Task SalesRepOrders_WithoutStatus_ReturnsAllStatuses()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        // No status argument → no status filter → all statuses returned.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2").And.Contain("ORD-NEW").And.Contain("ORD-CANCELLED");
    }

    [Fact]
    public async Task SalesRepOrders_ReturnsLocalizedRawStatus()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        // statusDisplayValue is the order's RAW status localized via X-API's LocalizedField. The cultureName argument
        // is copied to the UserContext by the builder, so it reaches the per-item resolver (StubLocalizableSettingService
        // renders "<raw> (<culture>)"), which also proves the culture actually propagated.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", cultureName:\"en-US\") { items { number status statusDisplayValue } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"status\":\"Cancelled\"");                        // raw status preserved
        json.Should().Contain("\"statusDisplayValue\":\"Cancelled (en-US)\""); // raw status localized in the requested culture
    }

    [Fact]
    public async Task SalesRepOrders_WithoutOrganizationId_ReturnsOrdersAcrossAssignedCustomers()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2"); // assigned to org-1 + org-2, NOT org-3
        // o-1 has no denormalized OrganizationName → organizationName falls back to a member lookup (→ "org-1").
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        // o-2 carries a denormalized OrganizationName → organizationName uses it directly (no lookup), distinct from the member name.
        SeedOrder(ctx, id: "o-2", org: "org-2", number: "ORD-2", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), organizationName: "Drift Inn Resort");
        SeedOrder(ctx, id: "o-3", org: "org-3", number: "ORD-3", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        // No organizationId → cross-customer dashboard: orders of every assigned customer; the unassigned org is excluded.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders { totalCount items { number organizationId organizationName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-1").And.Contain("ORD-2");
        json.Should().NotContain("ORD-3");
        // organizationId = the order's organization id.
        json.Should().Contain("\"organizationId\":\"org-1\"").And.Contain("\"organizationId\":\"org-2\"");
        // organizationName: fallback lookup for o-1 (member name "org-1"); the order's stored name for o-2.
        json.Should().Contain("\"organizationName\":\"org-1\"");          // resolved from the organization id
        json.Should().Contain("\"organizationName\":\"Drift Inn Resort\""); // used the value stored on the order
    }

    private static void SeedOrder(SalesRepTestContext ctx, string id, string org, string number, DateTime createdDate, string storeId = "B2B-store", int itemsCount = 0, string status = "New", string organizationName = null)
    {
        using var db = ctx.NewOrderDbContext();
        var order = new CustomerOrderEntity
        {
            Id = id,
            Number = number,
            OrganizationId = org,
            OrganizationName = organizationName,
            CustomerId = "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Status = status,
            Currency = "USD",
            Total = 123.45m,
            IsPrototype = false,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        };

        for (var i = 0; i < itemsCount; i++)
        {
            order.Items.Add(new LineItemEntity
            {
                Id = $"{id}-li-{i}",
                Currency = "USD",
                ProductId = $"prod-{i}",
                CatalogId = "catalog-1",
                Sku = $"SKU-{i}",
                Name = $"Product {i}",
                Quantity = 1,
                CreatedDate = createdDate,
                ModifiedDate = createdDate,
            });
        }

        db.Add(order);
        db.SaveChanges();
    }
}
