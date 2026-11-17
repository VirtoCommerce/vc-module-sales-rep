using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.XFrontend.Core.Layouts.Services;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.LayoutControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Dashboard;

/// <summary>
/// The hub dashboard's layout: saved under the hub's own scope through X-Frontend, and followed by what the hub's
/// widgets fetch. Each test saves a layout, which is kept per user, so each signs in as a rep of its own.
/// </summary>
[Collection(DashboardCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class DashboardLayoutTests(DashboardEnvironment env)
{
    private const string Week = "Orders placed · WEEK";

    [Fact(Timeout = 300_000)]
    public async Task HiddenStat_IsSavedForTheHubDashboard_AndItsPeriodIsNoLongerFetched()
    {
        var rep = await env.NewRepAsync(DashboardEnvironment.TailspinId);

        await using (var session = await env.OpenAsync(rep.UserId, DashboardEnvironment.DashboardPath))
        {
            await session.RunAsync(nameof(HiddenStat_IsSavedForTheHubDashboard_AndItsPeriodIsNoLongerFetched), async page =>
            {
                await EditLayout(page).ClickAsync(new() { Timeout = 60_000 });
                await HideStatAsync(page, Week);
                await SaveLayoutAsync(page);
            });
        }

        // Under the hub's scope and this store, read back through X-Frontend's own service.
        var layout = await env.RunExclusiveAsync(context => context.GetRequiredService<ILayoutService>()
            .GetLayoutAsync(rep.UserId, "salesRepDashboard", InProcessBackend.StoreId));
        layout.Regions.SelectMany(x => x.Blocks).Should().ContainSingle(x => x.Id == "orders_placed_week").Which.Hidden.Should().BeTrue();

        var traffic = new GraphQlTraffic();
        await using var reloaded = await env.OpenAsync(rep.UserId, DashboardEnvironment.DashboardPath, beforeLoad: traffic.AttachAsync);

        await reloaded.RunAsync($"{nameof(HiddenStat_IsSavedForTheHubDashboard_AndItsPeriodIsNoLongerFetched)}_Reloaded", async page =>
        {
            // The rep has no orders: a figure on the cards means the statistics have answered.
            await Expect(page.GetByText("€0.00").First).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText(Week, new() { Exact = true })).ToHaveCountAsync(0);
        });

        traffic.Sent("SalesRepCustomerOrderStatistics").Should().NotBeEmpty()
            .And.OnlyContain(x => !x.Variables.GetProperty("withWeek").GetBoolean());
    }

    [Fact(Timeout = 300_000)]
    public async Task RecentOrdersRows_AreSavedForTheHubDashboard_AndFetchedThatMany()
    {
        var rep = await env.NewRepAsync(DashboardEnvironment.TailspinId);
        var numbers = await env.SeedOrdersAsync(rep, 4);

        await using (var session = await env.OpenAsync(rep.UserId, DashboardEnvironment.DashboardPath))
        {
            await session.RunAsync(nameof(RecentOrdersRows_AreSavedForTheHubDashboard_AndFetchedThatMany), async page =>
            {
                await EditLayout(page).ClickAsync(new() { Timeout = 60_000 });

                // The field takes its value when it loses focus.
                var rows = page.GetByRole(AriaRole.Spinbutton, new() { Name = "Max rows for My recent orders, between 1 and 20", Exact = true });
                await rows.FillAsync("3");
                await rows.BlurAsync();

                await SaveLayoutAsync(page);
            });
        }

        var traffic = new GraphQlTraffic();
        await using var reloaded = await env.OpenAsync(rep.UserId, DashboardEnvironment.DashboardPath, beforeLoad: traffic.AttachAsync);

        await reloaded.RunAsync($"{nameof(RecentOrdersRows_AreSavedForTheHubDashboard_AndFetchedThatMany)}_Reloaded", async page =>
        {
            // The three newest; the oldest of the four no longer fits.
            await Expect(OrderLink(page, numbers[0])).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(OrderLink(page, numbers[1])).ToBeVisibleAsync();
            await Expect(OrderLink(page, numbers[2])).ToBeVisibleAsync();
            await Expect(OrderLink(page, numbers[3])).ToHaveCountAsync(0);
        });

        traffic.Sent("SalesRepOrders").Should().NotBeEmpty()
            .And.OnlyContain(x => x.Variables.GetProperty("first").GetInt32() == 3);
    }
}
