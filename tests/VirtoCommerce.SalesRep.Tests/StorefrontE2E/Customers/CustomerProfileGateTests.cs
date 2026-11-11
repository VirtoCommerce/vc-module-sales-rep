using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>
/// A customer's page asks for the customer's statistics only once the customer has been read and is one the rep serves:
/// an unserved, unknown or unreadable customer costs no statistics query.
/// </summary>
[Collection(CustomersCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class CustomerProfileGateTests(CustomersEnvironment env)
{
    private const string Customer = "SalesRepCustomer";
    private const string OrderStatistics = "SalesRepCustomerOrderStatistics";
    private const string CartStatistics = "SalesRepCustomerCartStatistics";

    [Theory(Timeout = 300_000)]
    [InlineData(CustomersEnvironment.AdventureWorksId)]
    [InlineData("e2e-org-missing")]
    public async Task UnservedOrUnknownCustomer_GetsNoStatisticsQuery(string organizationId)
    {
        var traffic = new GraphQlTraffic();
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{organizationId}", beforeLoad: traffic.AttachAsync);

        await session.RunAsync($"{nameof(UnservedOrUnknownCustomer_GetsNoStatisticsQuery)}_{organizationId}", async page =>
        {
            await Expect(page.GetByText("Customer not found or not in your customers.", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 60_000 });
        });

        // The read that found no customer went out; the statistics wait for a customer, so nothing will send them later.
        traffic.Sent(Customer).Should().NotBeEmpty();
        traffic.Sent(OrderStatistics).Should().BeEmpty();
        traffic.Sent(CartStatistics).Should().BeEmpty();
    }

    [Fact(Timeout = 300_000)]
    public async Task UnreadableCustomer_SaysSo_AndGetsNoStatisticsQuery()
    {
        var traffic = new GraphQlTraffic(failing: name => name == Customer);
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{CustomersEnvironment.FabrikamId}", beforeLoad: traffic.AttachAsync);

        await session.RunAsync(nameof(UnreadableCustomer_SaysSo_AndGetsNoStatisticsQuery), async page =>
        {
            await Expect(page.GetByText("This customer couldn't be loaded. Try refreshing the page.", new() { Exact = true }))
                .ToBeVisibleAsync(new() { Timeout = 60_000 });
        });

        traffic.Sent(OrderStatistics).Should().BeEmpty();
        traffic.Sent(CartStatistics).Should().BeEmpty();
    }

    [Fact(Timeout = 300_000)]
    public async Task ServedCustomer_GetsItsStatistics_OnceTheCustomerIsRead()
    {
        var traffic = new GraphQlTraffic();
        await using var session = await env.OpenAsync(env.RepUserId, $"/company/my-customers/{CustomersEnvironment.FabrikamId}", beforeLoad: traffic.AttachAsync);

        await session.RunAsync(nameof(ServedCustomer_GetsItsStatistics_OnceTheCustomerIsRead), async page =>
        {
            // ORD-1001 is new, three days old and $123.45, which is €98.76 at the harness's rate.
            await Expect(page.GetByText("€98.76 total", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText("of 1 created in the last 7 days", new() { Exact = true })).ToBeVisibleAsync();
        });

        var customerRead = traffic.FinishedAt(Customer);
        customerRead.Should().NotBeNull();
        traffic.Sent(OrderStatistics).Should().NotBeEmpty().And.OnlyContain(x => x.SentAt > customerRead);
        traffic.Sent(CartStatistics).Should().NotBeEmpty().And.OnlyContain(x => x.SentAt > customerRead);
    }
}
