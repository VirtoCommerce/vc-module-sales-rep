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
    public async Task MySalesReps_ReturnsRepsServingCallerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1")));
        // A rep serving only org-2 must NOT appear for an org-1 member.
        SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Other", "Rep", "other@test.com", "org-2")));

        // Caller is a member of org-1 (organization_id claim).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { mySalesReps { totalCount items { id fullName emails phones } } }",
            userId: "any-member",
            organizationId: "org-1");

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("jane@test.com");
        json.Should().Contain("Jane Rep");
        json.Should().NotContain("other@test.com");
        json.Should().Contain(rep.Id);
    }

    [Fact]
    public async Task MySalesReps_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { mySalesReps { totalCount items { id } } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task MyCustomers_ReturnsOrganizationsServedByCaller()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1", "org-2", "org-3");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1", "org-2")));

        // Caller is the rep (their security-account id).
        var json = await ctx.ExecuteGraphQlAsync(
            "query { myCustomers { totalCount items { organizationId organizationName } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("org-1");
        json.Should().Contain("org-2");
        json.Should().NotContain("org-3"); // the rep does not serve org-3
    }

    [Fact]
    public async Task MyCustomers_Anonymous_ReturnsAuthorizationError()
    {
        using var ctx = SalesRepTestContext.Create();

        var json = await ctx.ExecuteGraphQlAnonymousAsync(
            "query { myCustomers { totalCount items { organizationId } } }");

        json.Should().Contain("\"errors\"");
        json.Should().MatchRegex("(?i)anonym");
    }

    [Fact]
    public async Task MyCustomers_WithLastOrder_ReturnsMostRecentOrderPerOrganization()
    {
        using var ctx = SalesRepTestContext.Create();
        await ctx.SeedOrganizationsAsync("org-1");
        var rep = SalesRepTestContext.Unwrap(await ctx.Controller.Create(SimpleRep("Jane", "Rep", "jane@test.com", "org-1")));

        SeedOrder(ctx, id: "o-old", org: "org-1", number: "ORD-OLD", createdDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedOrder(ctx, id: "o-new", org: "org-1", number: "ORD-NEW", createdDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var json = await ctx.ExecuteGraphQlAsync(
            "query { myCustomers { items { organizationId lastOrder { number total currency } } } }",
            userId: rep.UserId);

        json.Should().NotContain("\"errors\"");
        json.Should().Contain("ORD-NEW");   // most recent
        json.Should().NotContain("ORD-OLD"); // older order is not the "last order"
        json.Should().Contain("123.45");    // Total must be hydrated, not 0
        json.Should().Contain("USD");
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
