using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
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
    public async Task SalesRepCustomers_WithUnrecognizedFilter_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        // The default customer-segment resolver defines no segments, so any segment name is unrecognized → fail-closed
        // (no customers), never "all served customers".
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(filter:\"vip\") { totalCount items { organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("org-1").And.NotContain("org-2");
    }

    [Fact]
    public async Task SalesRepCustomerFilterRules_DefaultIsAll()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Default customer segments: a single "All" baseline segment; a project registers its own resolver to add more.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerFilterRules(storeId:\"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomerFilterRules\":[{\"name\":\"All\",\"localizedName\":\"All\"}]");
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

        // Page 1 (the "name" sort rule is ascending): Acme, Globex
        var page1 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:2, after:\"0\", sort:\"name\") { totalCount pageInfo{ hasNextPage endCursor } items{ organizationName } } }",
            userId: rep.UserId);
        page1.Should().Contain("\"totalCount\":4").And.Contain("\"hasNextPage\":true");
        page1.Should().Contain("Acme").And.Contain("Globex");
        page1.Should().NotContain("Initech").And.NotContain("Umbrella");

        // Page 2 (after:2): Initech, Umbrella — no overlap with page 1
        var page2 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:2, after:\"2\", sort:\"name\") { totalCount pageInfo{ hasNextPage } items{ organizationName } } }",
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

        // The "name" sort rule is ascending: with first:1 the top row is Acme (first alphabetically), not Umbrella.
        var asc = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(first:1, sort:\"name\") { items{ organizationName } } }",
            userId: rep.UserId);
        asc.Should().Contain("Acme").And.NotContain("Umbrella");
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

        // The customer-detail card selects both fullName and name; the mapping populates name from the contact's
        // full name (they resolve to the same display value).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { primaryContact { id fullName name phones } phone } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("owner-1").And.Contain("\"fullName\":\"Olivia Owner\"");
        json.Should().Contain("\"name\":\"Olivia Owner\""); // name field resolves (mirrors fullName)
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
    public async Task SalesRepCustomer_PhoneOnly_ResolvesPrimaryContactForFallback()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedContactAsync("owner-1", c =>
        {
            c.FirstName = "Olivia";
            c.LastName = "Owner";
            c.Name = "Olivia Owner";
            c.Phones = ["+1-999-0000"];
        });
        await ctx.SeedOrganizationAsync("org-1", o => o.OwnerId = "owner-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Selecting `phone` but NOT `primaryContact` must still resolve the primary contact — the phone falls back
        // to it. Guards the gate's phone branch: dropping it would null the contact and lose the phone.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { phone } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("999-0000");   // resolved from the owner contact (the "+" is JSON-escaped)
    }

    [Fact]
    public async Task SalesRepCustomer_MapsAccountTypeIconUrlAndAddress()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1", o =>
        {
            o.BusinessCategory = "Retailer";
            o.IconUrl = "https://cdn.test/org-1.png";
            // CountryName + RegionName are set so MemberService.FillAddressNames doesn't call the (dataless)
            // CountriesService in the harness. The storefront formats the display string from these structured parts.
            o.Addresses = [new VirtoCommerce.CustomerModule.Core.Model.Address { Line1 = "1 Main St", City = "Seattle", RegionName = "WA", CountryName = "United States", CountryCode = "US", PostalCode = "98101", IsDefault = true }];
        });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { accountType iconUrl address { line1 city regionName postalCode isDefault } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"accountType\":\"Retailer\"");
        json.Should().Contain("\"iconUrl\":\"https://cdn.test/org-1.png\"");
        json.Should().Contain("\"line1\":\"1 Main St\"");
        json.Should().Contain("\"city\":\"Seattle\"");
        json.Should().Contain("\"regionName\":\"WA\"");
        json.Should().Contain("\"isDefault\":true");
    }

    [Fact]
    public async Task SalesRepCustomers_MapIconUrlAndAddress()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1", o =>
        {
            o.IconUrl = "https://cdn.test/org-1.png";
            o.Addresses = [new VirtoCommerce.CustomerModule.Core.Model.Address { Line1 = "1 Main St", City = "Seattle", RegionName = "WA", CountryName = "United States", CountryCode = "US", PostalCode = "98101", IsDefault = true }];
        });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Selecting `address` on the list must hydrate the org's Addresses (field-driven WithAddresses response
        // group), while iconUrl is a scalar that loads with Default. postalCode + zip are both selected by the My
        // Customers list; the storefront shows postalCode (zip is a legacy field that resolves but stays null here).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId iconUrl address { postalCode zip city regionName } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"iconUrl\":\"https://cdn.test/org-1.png\"");
        json.Should().Contain("\"city\":\"Seattle\"").And.Contain("\"regionName\":\"WA\"");
        json.Should().Contain("\"postalCode\":\"98101\"").And.Contain("\"zip\":"); // both fields resolve (zip selectable)
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
    public async Task CustomerSalesReps_LoadsEmailsAndPhonesWhenSelected()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("AcmeOrg");
        // CreateRep seeds the rep contact with an email + phone.
        await ctx.CreateRepAsync("Bea", "B2B", "bea@test.com", "AcmeOrg");

        // Selecting emails/phones must hydrate those collections (field-driven WithEmails | WithPhones); a query of
        // only scalar fields (see CustomerSalesReps_AreScopedByStore) leaves them on Default.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { items { fullName emails phones } } }",
            organizationId: "AcmeOrg");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("bea@test.com");
        json.Should().Contain("555-0100");   // the seeded phone (the "+" is JSON-escaped)
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
    public async Task SalesRepOrders_SortByTotal_OrdersByTotalInBothDirections()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // Distinct totals; created dates deliberately NOT in total order, so a total sort can't be faked by date.
        SeedOrder(ctx, id: "small", org: "org-1", number: "ORD-SMALL", createdDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), total: 50m);
        SeedOrder(ctx, id: "big", org: "org-1", number: "ORD-BIG", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), total: 5000m);
        SeedOrder(ctx, id: "mid", org: "org-1", number: "ORD-MID", createdDate: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), total: 500m);

        // Bare "total" → the rule's natural direction (biggest first).
        var largest = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", sort:\"total\") { items { number } } }",
            userId: rep.UserId);
        largest.Should().NotContain("\"errors\"");
        // Largest total first: BIG (5000) → MID (500) → SMALL (50), regardless of created date.
        largest.IndexOf("ORD-BIG", StringComparison.Ordinal).Should().BeLessThan(largest.IndexOf("ORD-MID", StringComparison.Ordinal));
        largest.IndexOf("ORD-MID", StringComparison.Ordinal).Should().BeLessThan(largest.IndexOf("ORD-SMALL", StringComparison.Ordinal));

        // "total:asc" reverses the reversible "total" rule to smallest first.
        var smallest = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", sort:\"total:asc\") { items { number } } }",
            userId: rep.UserId);
        smallest.Should().NotContain("\"errors\"");
        // Smallest total first: SMALL (50) → MID (500) → BIG (5000).
        smallest.IndexOf("ORD-SMALL", StringComparison.Ordinal).Should().BeLessThan(smallest.IndexOf("ORD-MID", StringComparison.Ordinal));
        smallest.IndexOf("ORD-MID", StringComparison.Ordinal).Should().BeLessThan(smallest.IndexOf("ORD-BIG", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepOrders_SortByRecentAscending_ReturnsError()
    {
        // "recent" is one-way (newest first only): an explicit opposite direction is rejected, not silently ignored.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", sort:\"recent:asc\") { items { number } } }",
            userId: rep.UserId);

        json.Should().Contain("\"errors\"");
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
    public async Task SalesRepOrderFilterRulees_ReturnsStatuses()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-processing", org: "org-1", number: "ORD-PROCESSING", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Processing");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        // Rule discovery is sales-rep-only: the caller must hold a granting membership (see the authorization gate).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // The real default resolver maps each status the store's orders use to a 1:1 rule (name == raw status).
        json.Should().Contain("\"name\":\"New\"")
            .And.Contain("\"name\":\"Processing\"")
            .And.Contain("\"name\":\"Cancelled\"");
        // "Failed" is in the configured Order.Status dictionary but no seeded order uses it → not offered.
        json.Should().NotContain("\"name\":\"Failed\"");
    }

    [Fact]
    public async Task SalesRepOrderFilterRulees_OnlyStatusesTheRepsOwnOrdersUse()
    {
        // The offered statuses must come from the same orders the list searches — the rep's served organizations AND
        // the orders the rep created. A status that only exists on someone else's order, or in an organization the rep
        // does not serve, would render a chip that shows "no orders match this filter".
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-unserved");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-mine", org: "org-1", number: "ORD-MINE", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-other-rep", org: "org-1", number: "ORD-OTHER-REP", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Processing", createdByUserId: "other-rep");
        SeedOrder(ctx, id: "o-unserved", org: "org-unserved", number: "ORD-UNSERVED", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"New\"");
        json.Should().NotContain("\"name\":\"Processing\"").And.NotContain("\"name\":\"Cancelled\"");
    }

    [Fact]
    public async Task SalesRepOrderFilterRulees_OrganizationId_ScopesToThatCustomer()
    {
        // The customer page knows the organization, so the chips must reflect that customer's orders only.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-2", org: "org-2", number: "ORD-2", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Processing");

        var scoped = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\", organizationId:\"org-2\") { name } }",
            userId: rep.UserId);

        scoped.Should().NotContain("\"errors\"");
        scoped.Should().Contain("\"name\":\"Processing\"").And.NotContain("\"name\":\"New\"");

        // Omitting it keeps the rep-wide vocabulary (the dashboard case).
        var all = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name } }",
            userId: rep.UserId);

        all.Should().Contain("\"name\":\"New\"").And.Contain("\"name\":\"Processing\"");

        // An organization the rep does not serve narrows to nothing, like the list does.
        var unserved = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\", organizationId:\"org-outsider\") { name } }",
            userId: rep.UserId);

        unserved.Should().NotContain("\"errors\"");
        unserved.Should().NotContain("\"name\":");
    }

    [Fact]
    public async Task SalesRepOrderFilterRulees_Period_ScopesToStatusesUsedInThatWindow()
    {
        // The chips must match the list the caller is looking at: with a period selected, a status only older orders
        // carry is not offered (clicking it would return nothing).
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-in", org: "org-1", number: "ORD-IN", createdDate: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-out", org: "org-1", number: "ORD-OUT", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        const string period = "period: { from: \"2026-06-01T00:00:00Z\", to: \"2026-07-01T00:00:00Z\" }";

        var scoped = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepOrderFilterRules(storeId:\"B2B-store\", {period}) {{ name }} }}",
            userId: rep.UserId);

        scoped.Should().NotContain("\"errors\"");
        scoped.Should().Contain("\"name\":\"New\"").And.NotContain("\"name\":\"Cancelled\"");

        // The offered status does return rows for that same period.
        var orders = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepOrders(storeId:\"B2B-store\", filter:\"New\", {period}) {{ totalCount items {{ number }} }} }}",
            userId: rep.UserId);

        orders.Should().Contain("\"totalCount\":1").And.Contain("ORD-IN");

        // Without a period both statuses are offered (all dates).
        var allDates = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name } }",
            userId: rep.UserId);

        allDates.Should().Contain("\"name\":\"New\"").And.Contain("\"name\":\"Cancelled\"");
    }

    [Fact]
    public async Task SalesRepOrderFilterRulees_OffersStatusesMissingFromTheDictionary()
    {
        // The vocabulary is read from the orders themselves, so a status that arrived with an order from outside the
        // platform (an ERP/3rd-party sync) is filterable without touching the Order.Status dictionary first.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-erp", org: "org-1", number: "ORD-ERP", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "AwaitingErp");

        var rules = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        rules.Should().NotContain("\"errors\"");
        rules.Should().Contain("\"name\":\"AwaitingErp\"").And.Contain("\"localizedName\":\"AwaitingErp\"");

        // ...and selecting it filters the list, like any other status rule.
        var orders = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(storeId:\"B2B-store\", filter:\"AwaitingErp\") { totalCount items { number } } }",
            userId: rep.UserId);

        orders.Should().NotContain("\"errors\"");
        orders.Should().Contain("\"totalCount\":1").And.Contain("ORD-ERP").And.NotContain("ORD-NEW");
    }

    [Fact]
    public async Task SalesRepOrderFilterRules_CompositeOverride_ExposesGroupedStatus()
    {
        // A project override (CompositeOrderFilterRuleResolver) adds a composite "Inactive" → { Cancelled, Failed }
        // rule on top of the real 1:1 statuses; the discovery query must surface it with its localized label.
        using var ctx = SalesRepTestContext.Create(OrderFilterRuleOverride.WithCompositeInactiveStatus);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name localizedName } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"name\":\"New\"");                                // base 1:1 statuses still present
        json.Should().Contain("\"name\":\"Inactive\"").And.Contain("Not active"); // composite status, localized label
    }

    [Fact]
    public async Task SalesRepOrderFilterRulees_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { salesRepOrderFilterRules(storeId:\"B2B-store\") { name } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task SalesRepOrders_FiltersBySelectedStatus_ResolvesComposite()
    {
        // A composite "Inactive" → { Cancelled, Failed } rule is a project-override capability of the real resolver.
        using var ctx = SalesRepTestContext.Create(OrderFilterRuleOverride.WithCompositeInactiveStatus);
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");
        SeedOrder(ctx, id: "o-failed", org: "org-1", number: "ORD-FAILED", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Failed");

        // "Inactive" is a composite rule -> [Cancelled, Failed]; "New" must be excluded.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", filter:\"Inactive\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("ORD-CANCELLED").And.Contain("ORD-FAILED");
        json.Should().NotContain("ORD-NEW");
    }

    [Fact]
    public async Task SalesRepOrders_FiltersBySelectedStatus_SingleStatus()
    {
        // Default (real) resolver: each configured status is a 1:1 rule; filtering by one status returns only it.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", filter:\"Cancelled\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-CANCELLED").And.NotContain("ORD-NEW");
    }

    [Fact]
    public async Task SalesRepOrders_WithUnrecognizedStatus_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // Several orders across statuses — if an unrecognized-status filter were silently dropped (the bug), ALL of
        // these would come back; with the fix, none do.
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");
        SeedOrder(ctx, id: "o-processing", org: "org-1", number: "ORD-PROCESSING", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), status: "Processing");

        // "foo" is not one of this store's selectable status options, so the status service resolves it to no
        // underlying order status. The filter must then return nothing — mirroring the reported case where filtering
        // by a status the store doesn't define (e.g. "Failed"/"Inactive") wrongly returned every order.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", filter:\"foo\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-NEW").And.NotContain("ORD-CANCELLED").And.NotContain("ORD-PROCESSING");
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

    [Fact]
    public async Task SalesRepOrders_ExcludesOrdersNotCreatedByRep()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // Same customer org: one order created by this rep, one created by someone else (the customer directly, or
        // another rep). Only the rep's own order must be returned.
        SeedOrder(ctx, id: "o-mine", org: "org-1", number: "ORD-MINE", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-other", org: "org-1", number: "ORD-OTHER", createdDate: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc), createdByUserId: "another-user");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-MINE").And.NotContain("ORD-OTHER");
    }

    [Fact]
    public async Task SalesRepCustomers_LastOrder_ReturnsRepsOwnLatestNotCustomers()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // A NEWER order created by someone else must NOT become the customer's "last order" — only the rep's own
        // latest order counts, even if the customer has a more recent order from another source.
        SeedOrder(ctx, id: "o-rep", org: "org-1", number: "ORD-REP", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-other", org: "org-1", number: "ORD-OTHER", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), createdByUserId: "another-user");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId lastOrder { number } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-REP").And.NotContain("ORD-OTHER");
    }

    [Fact]
    public async Task SalesRepCustomers_LastOrder_IsPerOrganization_NotGlobalLatest()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-a", "org-b");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-a", "org-b");
        // Each org has its own older + latest order; org-b's latest is the GLOBALLY newest. A regression that
        // returns the global latest for every organization (broken per-org grouping in the latest-per-org query,
        // or wrong DataLoader keying) would put ORD-B-NEW on both rows and drop ORD-A-NEW.
        SeedOrder(ctx, id: "o-a-old", org: "org-a", number: "ORD-A-OLD", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-a-new", org: "org-a", number: "ORD-A-NEW", createdDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-b-old", org: "org-b", number: "ORD-B-OLD", createdDate: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-b-new", org: "org-b", number: "ORD-B-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(sort:\"name\") { items { organizationId lastOrder { number } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Each organization's row carries its OWN latest order — one occurrence of each, in org sort order.
        json.Should().Contain("ORD-A-NEW").And.Contain("ORD-B-NEW");
        json.Should().NotContain("ORD-A-OLD").And.NotContain("ORD-B-OLD");
        // org-a sorts first; its lastOrder (ORD-A-NEW) must appear before org-b's — pins the per-org pairing,
        // not just "both numbers occur somewhere".
        json.IndexOf("ORD-A-NEW", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("ORD-B-NEW", StringComparison.Ordinal));
        json.IndexOf("ORD-B-NEW", StringComparison.Ordinal).Should().Be(json.LastIndexOf("ORD-B-NEW", StringComparison.Ordinal),
            "the globally newest order must not be duplicated onto other organizations' rows");
    }

    [Fact]
    public async Task SalesRepCustomers_LastOrder_IsNullForOrganizationWithoutOrders()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // No orders seeded at all — the row must come back with lastOrder:null, not an error (the loader's
        // dictionary simply has no entry for the org).

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { totalCount items { organizationId lastOrder { number } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("\"lastOrder\":null");
    }

    // ---- caller states: authenticated but not a rep / no organization claim ----

    [Fact]
    public async Task SalesRepCustomers_ForNonRepCaller_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1"); // data exists, but not for this caller

        // An authenticated user with no memberships at all (a regular customer hitting the endpoint) gets an
        // empty result, not an error.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { totalCount items { organizationId } } }",
            userId: "not-a-rep");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("org-1");
    }

    [Fact]
    public async Task SalesRepOrders_ForNonRepCaller_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // The order belongs to the rep's org; the non-rep caller below must not see it.
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders { totalCount items { number } } }",
            userId: "not-a-rep");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-1");
    }

    [Fact]
    public async Task CustomerSalesReps_WithoutOrganizationClaim_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // A private (non-organization) customer has no organization_id claim — the common storefront case.
        // Must yield an empty list, not an error.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { customerSalesReps { totalCount items { id } } }",
            userId: "private-customer");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
    }

    // ---- stale-data fallbacks on salesRepCustomer ----

    [Fact]
    public async Task SalesRepCustomer_WhenOrganizationDeleted_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        // Delete the organization member; the membership row survives (no FK), leaving a stale membership that
        // points at a gone organization. The query must return null, not throw.
        await ctx.GetRequiredService<VirtoCommerce.CustomerModule.Core.Services.IMemberService>().DeleteAsync(["org-1"]);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { organizationId } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomer\":null");
    }

    [Fact]
    public async Task SalesRepCustomer_WithDanglingOwnerId_FallsBackToFirstContact()
    {
        using var ctx = SalesRepTestContext.Create();
        // The organization's OwnerId points at a contact that no longer exists — the primary contact must fall
        // back to the first contact member (here the rep) instead of returning nothing.
        await ctx.SeedOrganizationAsync("org-1", o => o.OwnerId = "ghost-owner");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { primaryContact { id fullName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain(rep.Id).And.Contain("Jane Rep");
        json.Should().NotContain("ghost-owner");
    }

    [Fact]
    public async Task SalesRepCustomer_PhoneFallsBackToOrganizationPhone()
    {
        using var ctx = SalesRepTestContext.Create();
        // Owner contact WITHOUT phones + organization with its own phone: the phone must fall back to the
        // organization's (the second half of the contact-then-organization fallback chain).
        await ctx.SeedContactAsync("owner-1", c =>
        {
            c.FirstName = "Olivia";
            c.LastName = "Owner";
            c.Name = "Olivia Owner";
        });
        await ctx.SeedOrganizationAsync("org-1", o =>
        {
            o.OwnerId = "owner-1";
            o.Phones = ["+1-777-0000"];
        });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"org-1\") { phone } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("777-0000"); // the organization's phone (the "+" is JSON-escaped)
    }

    // ---- data caveats and explicit argument edges ----

    [Fact]
    public async Task Queries_TolerateMembershipWithNullOrganizationId()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Real databases contain memberships whose OrganizationId is null; give the rep one carrying the same
        // granting role. Queries must keep working and silently exclude it.
        var grantingRole = AbstractTypeFactory<Role>.TryCreateInstance();
        grantingRole.Id = rep.RoleId;
        grantingRole.Name = rep.RoleName;
        await ctx.AddMembershipAsync(rep.UserId, organizationId: null, grantingRole);

        var customers = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { totalCount items { organizationId } } }",
            userId: rep.UserId);
        customers.Should().NotContain("\"errors\"");
        customers.Should().Contain("\"totalCount\":1", "the null-organization membership must be excluded, not crash the query");
        customers.Should().Contain("org-1");

        var orders = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders { totalCount items { number } } }",
            userId: rep.UserId);
        orders.Should().NotContain("\"errors\"");
        orders.Should().Contain("\"totalCount\":1").And.Contain("ORD-1");
    }

    [Fact]
    public async Task SalesRepCustomer_NonExistentOrganizationId_ReturnsNull()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomer(organizationId:\"no-such-org\") { organizationId } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"salesRepCustomer\":null");
    }

    [Fact]
    public async Task SalesRepOrders_NonExistentOrganizationId_ReturnsEmpty()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"no-such-org\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":0");
        json.Should().NotContain("ORD-1");
    }

    [Fact]
    public async Task SalesRepOrders_EmptyFilter_ReturnsAllOrders()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), status: "New");
        SeedOrder(ctx, id: "o-cancelled", org: "org-1", number: "ORD-CANCELLED", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");

        // An explicit empty filter string means "no filter" (like omitting the argument) — not "match nothing".
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(organizationId:\"org-1\", filter:\"\") { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2").And.Contain("ORD-NEW").And.Contain("ORD-CANCELLED");
    }

    // ---- VCST-5309 Phase 1: units, account fields, "All" segment, orders period, sort rules, inline statistics ----

    [Fact]
    public async Task SalesRepOrders_ItemsQuantity_SumsLineItemQuantities()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        // 2 line items, 3 units each → itemsCount = 2 (lines), itemsQuantity = 6 (units).
        SeedOrder(ctx, id: "o1", org: "org-1", number: "ORD-1", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), itemsCount: 2, quantityPerItem: 3);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders { items { number itemsCount itemsQuantity } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"itemsCount\":2");
        json.Should().Contain("\"itemsQuantity\":6");
    }

    [Fact]
    public async Task SalesRepCustomers_ExposesAccountTypeAndAccountId()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationAsync("org-1", o => { o.BusinessCategory = "Garden Center"; o.OuterId = "ACC-303648"; });
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId accountType accountId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"accountType\":\"Garden Center\"");
        json.Should().Contain("\"accountId\":\"ACC-303648\"");
    }

    [Fact]
    public async Task SalesRepCustomers_AllFilter_ReturnsAllServedCustomers()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2");

        // The default customer-segment resolver's single "All" rule is a passthrough — every served customer.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(filter:\"All\") { totalCount items { organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":2");
        json.Should().Contain("org-1").And.Contain("org-2");
    }

    [Fact]
    public async Task SalesRepCustomers_TotalCountOnly_ReturnsServedCount()
    {
        // The "My customers" sidebar badge selects ONLY totalCount (no items) — the count path must resolve without
        // any item hydration.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1", "org-2", "org-3");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(storeId:\"B2B-store\") { totalCount } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":3");
    }

    [Fact]
    public async Task SalesRepOrders_Period_FiltersByCreatedDate()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "in", org: "org-1", number: "ORD-IN", createdDate: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "out", org: "org-1", number: "ORD-OUT", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrders(period:{ from:\"2026-06-01T00:00:00Z\", to:\"2026-07-01T00:00:00Z\" }) { totalCount items { number } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"totalCount\":1");
        json.Should().Contain("ORD-IN").And.NotContain("ORD-OUT");
    }

    [Fact]
    public async Task SalesRepOrderSortRules_DefaultIsRecent()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepOrderSortRules { name localizedName defaultDirection supportsDirection } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // The two built-in orderings: "recent" (default) and the bidirectional "total".
        json.Should().Contain("\"name\":\"recent\"").And.Contain("\"name\":\"total\"");
        // Direction metadata drives the storefront's header-sort toggle: both default desc; "total" reverses, "recent" doesn't.
        json.Should().Contain("\"defaultDirection\":\"desc\"").And.Contain("\"supportsDirection\":true").And.Contain("\"supportsDirection\":false");
    }

    [Fact]
    public async Task SalesRepCustomerSortRules_ExposesDefaultOrderings()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerSortRules { name } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("my-last-orders").And.Contain("ytd-purchases").And.Contain("\"name\":\"name\"");
    }

    [Fact]
    public async Task SalesRepCustomers_SortByMyLastOrders_RanksByRepsMostRecentOrder()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-a", "org-b", "org-c");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-a", "org-b", "org-c");
        SeedOrder(ctx, id: "a", org: "org-a", number: "ORD-A", createdDate: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "b", org: "org-b", number: "ORD-B", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "c", org: "org-c", number: "ORD-C", createdDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        // Data-isolation: a foreign rep's newer order in org-a must NOT pull org-a to the top (only the rep's own
        // orders drive the ranking).
        SeedOrder(ctx, id: "foreign", org: "org-a", number: "ORD-FOREIGN", createdDate: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), createdByUserId: "other-rep");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(sort:\"my-last-orders\") { items { organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Newest rep order first: org-b (Jun) → org-c (Apr) → org-a (Feb; the Dec foreign order is ignored).
        json.IndexOf("org-b", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-c", StringComparison.Ordinal));
        json.IndexOf("org-c", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-a", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepCustomers_SortByYtdPurchases_RanksByThisYearsOrderTotal()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-high", "org-mid", "org-low");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-high", "org-mid", "org-low");

        // "ytd purchases" ranks by this year's order total (biggest first). Seed one current-year order per org with
        // distinct totals so the ranking is unambiguous regardless of the year the suite runs in.
        var thisYear = new DateTime(DateTime.UtcNow.Year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedOrder(ctx, id: "h", org: "org-high", number: "ORD-H", createdDate: thisYear, total: 300m);
        SeedOrder(ctx, id: "m", org: "org-mid", number: "ORD-M", createdDate: thisYear, total: 200m);
        SeedOrder(ctx, id: "l", org: "org-low", number: "ORD-L", createdDate: thisYear, total: 100m);
        // Data-isolation: a foreign rep's large order in the lowest org must NOT lift it (only the rep's own orders count).
        SeedOrder(ctx, id: "foreign", org: "org-low", number: "ORD-FOREIGN", createdDate: thisYear, total: 10000m, createdByUserId: "other-rep");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(sort:\"ytd-purchases\") { items { organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Largest YTD total first: org-high (300) → org-mid (200) → org-low (100; the 10000 foreign order is ignored).
        json.IndexOf("org-high", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-mid", StringComparison.Ordinal));
        json.IndexOf("org-mid", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-low", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepCustomers_SortByName_Descending_ReversesOrder()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("Acme", "Globex", "Umbrella");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "Acme", "Globex", "Umbrella");

        // "name:desc" reverses the "name" rule's natural A→Z.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(sort:\"name:desc\") { items { organizationName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Z→A: Umbrella → Globex → Acme.
        json.IndexOf("Umbrella", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("Globex", StringComparison.Ordinal));
        json.IndexOf("Globex", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("Acme", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepCustomers_SortByYtdPurchases_Ascending_RanksSmallestFirst()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-high", "org-mid", "org-low");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-high", "org-mid", "org-low");
        var thisYear = new DateTime(DateTime.UtcNow.Year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedOrder(ctx, id: "h", org: "org-high", number: "ORD-H", createdDate: thisYear, total: 300m);
        SeedOrder(ctx, id: "m", org: "org-mid", number: "ORD-M", createdDate: thisYear, total: 200m);
        SeedOrder(ctx, id: "l", org: "org-low", number: "ORD-L", createdDate: thisYear, total: 100m);

        // "ytd-purchases:asc" flips the rule from its natural biggest-first to smallest-first.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers(sort:\"ytd-purchases:asc\") { items { organizationId } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        // Smallest YTD total first: org-low (100) → org-mid (200) → org-high (300).
        json.IndexOf("org-low", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-mid", StringComparison.Ordinal));
        json.IndexOf("org-mid", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("org-high", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SalesRepCustomers_OrderStatistics_InlinePerRow_CountsOnlyRepsOwnOrders()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "mine", org: "org-1", number: "ORD-MINE", createdDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        // Data-isolation: a foreign rep's order in the same organization must not be counted in this rep's figures.
        SeedOrder(ctx, id: "foreign", org: "org-1", number: "ORD-F", createdDate: new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), createdByUserId: "other-rep");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomers { items { organizationId orderStatistics(from:\"2026-01-01T00:00:00Z\", to:\"2026-12-31T00:00:00Z\") { count total { amount } } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("\"count\":1");   // only the rep's own order
        json.Should().Contain("123.45");        // its total, not doubled by the foreign order
    }

    [Fact]
    public async Task SalesRepCustomers_OrderStatistics_TwoAliasedRanges_ResolveIndependentlyPerRow()
    {
        // The My Customers list selects TWO aliased inline orderStatistics ranges per row (ytd + lastYear), coalesced
        // by the DataLoader. Each alias must resolve to its own window's figures on the same row.
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "y1", org: "org-1", number: "ORD-Y1", createdDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "y2", org: "org-1", number: "ORD-Y2", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "ly", org: "org-1", number: "ORD-LY", createdDate: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            """
            query { salesRepCustomers { items { organizationId
              ytd:      orderStatistics(from:"2026-01-01T00:00:00Z", to:"2026-12-31T00:00:00Z") { count total { amount } }
              lastYear: orderStatistics(from:"2025-01-01T00:00:00Z", to:"2025-12-31T00:00:00Z") { count total { amount } }
            } } }
            """,
            userId: rep.UserId);

        var row = SalesRepTestContext.Node(json, "salesRepCustomers").GetProperty("items").EnumerateArray().Single();
        var ytd = row.GetProperty("ytd");
        var lastYear = row.GetProperty("lastYear");
        ytd.GetProperty("count").GetInt32().Should().Be(2);                                    // the two 2026 orders
        ytd.GetProperty("total").GetProperty("amount").GetDecimal().Should().Be(246.90m);      // 123.45 * 2
        lastYear.GetProperty("count").GetInt32().Should().Be(1);                               // the single 2025 order
        lastYear.GetProperty("total").GetProperty("amount").GetDecimal().Should().Be(123.45m);
    }

    [Fact]
    public async Task SalesRepCustomerOrderStatistics_Period_ExposesFirstOrderDate()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = await ctx.CreateRepAsync("Jane", "Rep", "jane@test.com", "org-1");
        SeedOrder(ctx, id: "first", org: "org-1", number: "ORD-FIRST", createdDate: new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "last", org: "org-1", number: "ORD-LAST", createdDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        // An unbounded period's firstOrderDate is the "customer since" value; lastOrderDate its most recent order.
        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepCustomerOrderStatistics(organizationId:\"org-1\") { lifetime: period { firstOrderDate lastOrderDate } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("2024-05-01"); // firstOrderDate
        json.Should().Contain("2026-05-01"); // lastOrderDate
    }

    private static void SeedOrder(SalesRepTestContext ctx, string id, string org, string number, DateTime createdDate, string storeId = "B2B-store", int itemsCount = 0, int quantityPerItem = 1, string status = "New", string organizationName = null, string createdByUserId = null, decimal total = 123.45m)
    {
        using var db = ctx.NewOrderDbContext();
        var order = new CustomerOrderEntity
        {
            Id = id,
            Number = number,
            OrganizationId = org,
            OrganizationName = organizationName,
            // A rep-created order records the rep's user id as CustomerId (the value the queries filter on). Default
            // to the test's rep so seeded orders count as "created by the rep"; pass createdByUserId to simulate an
            // order created by someone else.
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Status = status,
            Currency = "USD",
            Total = total,
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
                Quantity = quantityPerItem,
                CreatedDate = createdDate,
                ModifiedDate = createdDate,
            });
        }

        db.Add(order);
        db.SaveChanges();
    }
}
