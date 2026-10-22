using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

[Collection(CustomersCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class MyCustomersTests(CustomersEnvironment env)
{
    [Fact(Timeout = 300_000)]
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
        var memberships = await env.Backend.RunExclusiveAsync(context => context.GetMembershipsAsync(env.RepUserId));
        memberships.Select(x => x.OrganizationId).Should().BeEquivalentTo([CustomersEnvironment.FabrikamId, CustomersEnvironment.ContosoId]);
    }

    [Fact(Timeout = 300_000)]
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

    [Fact(Timeout = 300_000)]
    public async Task QuickActions_OnAServedCustomer_OfferScheduleCallAndSendEmail()
    {
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{CustomersEnvironment.FabrikamId}");

        await session.RunAsync(nameof(QuickActions_OnAServedCustomer_OfferScheduleCallAndSendEmail), async page =>
        {
            // Both wait for the customer to load; the other two stay placeholders.
            await Expect(QuickAction(page, "Send email")).ToBeEnabledAsync(new() { Timeout = 60_000 });
            await Expect(QuickAction(page, "Schedule call")).ToBeEnabledAsync();
            await Expect(QuickAction(page, "Order on behalf")).ToBeDisabledAsync();
            await Expect(QuickAction(page, "Create quote")).ToBeDisabledAsync();

            await QuickAction(page, "Send email").ClickAsync();
            var message = Dialog(page, "Send a message");
            await CancelButton(message).ClickAsync();
            await Expect(message).ToHaveCountAsync(0);

            await QuickAction(page, "Schedule call").ClickAsync();
            var call = Dialog(page, "Schedule call");
            await Expect(Field(call, "Customer")).ToHaveValueAsync(CustomersEnvironment.FabrikamName);
            await CancelButton(call).ClickAsync();
            await Expect(call).ToHaveCountAsync(0);
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task UnservedCustomerPage_SaysNotFound()
    {
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{CustomersEnvironment.AdventureWorksId}");

        await session.RunAsync(nameof(UnservedCustomerPage_SaysNotFound), async page =>
        {
            await Expect(page.GetByText("Customer not found or not in your customers.", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText("Quick actions", new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(QuickAction(page, "Schedule call")).ToHaveCountAsync(0);
        });
    }
}
