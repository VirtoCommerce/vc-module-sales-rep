using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Dashboard;

/// <summary>What the hub dashboard's widgets show: the current store's orders only, and their own failures.</summary>
[Collection(DashboardCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class DashboardWidgetsTests(DashboardEnvironment env)
{
    [Fact(Timeout = 300_000)]
    public async Task Dashboard_CoversTheCurrentStoreOnly()
    {
        var traffic = new GraphQlTraffic();
        await using var session = await env.OpenAsync(env.RepUserId, DashboardEnvironment.DashboardPath, beforeLoad: traffic.AttachAsync);

        await session.RunAsync(nameof(Dashboard_CoversTheCurrentStoreOnly), async page =>
        {
            await Expect(OrderLink(page, DashboardEnvironment.StoreOrderNumber)).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(OrderLink(page, DashboardEnvironment.OtherStoreOrderNumber)).ToHaveCountAsync(0);

            // Both orders are new and inside every period: $200 is €160.00 at the harness's rate, and €220.00 would
            // count the other store's $75 too.
            await Expect(page.GetByText("€160.00 total", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("€220.00")).ToHaveCountAsync(0);
        });

        traffic.Sent("SalesRepOrders").Should().NotBeEmpty()
            .And.OnlyContain(x => x.Variables.GetProperty("storeId").GetString() == InProcessBackend.StoreId);
        traffic.Sent("SalesRepCustomerOrderStatistics").Should().NotBeEmpty()
            .And.OnlyContain(x => x.Variables.GetProperty("storeId").GetString() == InProcessBackend.StoreId);
    }

    [Fact(Timeout = 300_000)]
    public async Task FailedHubQueries_ShowInTheirWidgets_AndRaiseNoErrorToast()
    {
        var traffic = new GraphQlTraffic(failing: name => name.StartsWith("SalesRep", StringComparison.Ordinal));
        await using var session = await env.OpenAsync(env.RepUserId, DashboardEnvironment.DashboardPath, beforeLoad: traffic.AttachAsync);

        await session.RunAsync(nameof(FailedHubQueries_ShowInTheirWidgets_AndRaiseNoErrorToast), async page =>
        {
            await Expect(page.GetByText("Couldn't load orders", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText("Couldn't load top sellers", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Couldn't load tasks", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Couldn't load activity", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Couldn't load", new() { Exact = true }).First).ToBeVisibleAsync();

            // The storefront's error toasts carry this button. A query raises one as its failure arrives, before its
            // widget can show it, so none has been raised by now.
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Report a problem", Exact = true })).ToHaveCountAsync(0);
        });

        traffic.Sent("SalesRepOrders").Should().NotBeEmpty();
    }
}
