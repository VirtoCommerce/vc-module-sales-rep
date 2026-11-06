using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.XFrontend.Core.Layouts.Services;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.LayoutControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>
/// A customer's page keeps its own layout, apart from the dashboard's, and what its cards fetch follows it. The test
/// saves a layout, which is kept per user, so it signs in as a rep of its own.
/// </summary>
[Collection(CustomersCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class CustomerProfileLayoutTests(CustomersEnvironment env)
{
    private const string AverageOrderValue = "Avg order value";
    private const string FabrikamPath = $"/company/my-customers/{CustomersEnvironment.FabrikamId}";

    [Fact(Timeout = 300_000)]
    public async Task HiddenStat_IsSavedForTheCustomerPage_AndNoLongerFetched()
    {
        var rep = await env.NewRepAsync(CustomersEnvironment.FabrikamId);

        await using (var session = await env.OpenAsync(rep.UserId, FabrikamPath))
        {
            await session.RunAsync(nameof(HiddenStat_IsSavedForTheCustomerPage_AndNoLongerFetched), async page =>
            {
                await EditLayout(page).ClickAsync(new() { Timeout = 60_000 });
                await HideStatAsync(page, AverageOrderValue);
                await SaveLayoutAsync(page);
            });
        }

        // Under the customer page's scope and this store, read back through X-Frontend's own service.
        var layout = await env.RunExclusiveAsync(context => context.GetRequiredService<ILayoutService>()
            .GetLayoutAsync(rep.UserId, "salesRepCustomerProfile", InProcessBackend.StoreId));
        layout.Regions.SelectMany(x => x.Blocks).Should().ContainSingle(x => x.Id == "aov").Which.Hidden.Should().BeTrue();

        var traffic = new GraphQlTraffic();
        await using var reloaded = await env.OpenAsync(rep.UserId, FabrikamPath, beforeLoad: traffic.AttachAsync);

        await reloaded.RunAsync($"{nameof(HiddenStat_IsSavedForTheCustomerPage_AndNoLongerFetched)}_Reloaded", async page =>
        {
            // The rep placed no orders: a figure on the cards means the statistics have answered.
            await Expect(page.GetByText("€0.00").First).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(page.GetByText(AverageOrderValue, new() { Exact = true })).ToHaveCountAsync(0);
        });

        traffic.Sent("SalesRepCustomerOrderStatistics").Should().NotBeEmpty()
            .And.OnlyContain(x => !x.Variables.GetProperty("withAverageOrderValue").GetBoolean());
    }
}
