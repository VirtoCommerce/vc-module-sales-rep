using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

[Collection(CustomersCollection.Name)]
[Trait("Category", "StorefrontE2E")]
public sealed class MyCustomersTests(CustomersEnvironment env)
{
    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task MyCustomers_ListsEveryServedOrganization_WithTheRepsLatestOrder()
    {
        await using var session = await env.OpenAsync(env.RepUserId, "/company/my-customers");

        await session.RunAsync(nameof(MyCustomers_ListsEveryServedOrganization_WithTheRepsLatestOrder), async page =>
        {
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = CustomersEnvironment.FabrikamName }))
                .ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = CustomersEnvironment.ContosoName })).ToBeVisibleAsync();
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = "#" + CustomersEnvironment.OrderNumber })).ToBeVisibleAsync();
        });

        // The same facts, read straight from the database the storefront just rendered from.
        var memberships = await env.Context.GetMembershipsAsync(env.RepUserId);
        memberships.Select(x => x.OrganizationId).Should().BeEquivalentTo([CustomersEnvironment.FabrikamId, CustomersEnvironment.ContosoId]);
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task CustomerProfile_ShowsTheSeededOrganizationDetails()
    {
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{CustomersEnvironment.FabrikamId}");

        await session.RunAsync(nameof(CustomerProfile_ShowsTheSeededOrganizationDetails), async page =>
        {
            await Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1, Name = CustomersEnvironment.FabrikamName }))
                .ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText(CustomersEnvironment.FabrikamOwnerName)).ToBeVisibleAsync();
            await Expect(page.GetByText(CustomersEnvironment.FabrikamOwnerPhone)).ToBeVisibleAsync();

            // The profile's recent-orders widget links the bare number (the customers list prefixes it with '#').
            await Expect(page.GetByRole(AriaRole.Link, new() { Name = CustomersEnvironment.OrderNumber, Exact = true })).ToBeVisibleAsync();
        });
    }
}
