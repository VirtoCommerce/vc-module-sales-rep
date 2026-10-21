using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.WithoutTaskManagement;

/// <summary>
/// Without the task management module the module still starts (VCST-6399), and the hub hides every task surface but
/// one: "Schedule call" stays on the customer's page, disabled.
/// </summary>
[Collection(WithoutTaskManagementCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class WithoutTaskManagementTests(WithoutTaskManagementEnvironment env)
{
    [Fact(Timeout = 300_000)]
    public async Task WithoutTaskManagement_ScheduleCallIsDisabled()
    {
        await using (var session = await OpenAsync($"/company/my-customers/{WithoutTaskManagementEnvironment.NorthwindId}"))
        {
            await session.RunAsync(nameof(WithoutTaskManagement_ScheduleCallIsDisabled), async page =>
            {
                // Send email waits for the customer as Schedule call does: only the missing module keeps the second disabled.
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Send email", Exact = true })).ToBeEnabledAsync(new() { Timeout = 60_000 });
                await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true })).ToBeDisabledAsync();

                // The hub's menu goes from My customers to My activity, with no Tasks between them.
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "My activity", Exact = true })).ToBeVisibleAsync();
                await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Tasks", Exact = true })).ToHaveCountAsync(0);
            });
        }

        // The tasks page, opened directly, sends the rep to the dashboard, which has no tasks widget. In a session of its
        // own: reloading a page that may still be fetching its modules ran out of the browser's request slots.
        await using var tasks = await OpenAsync("/company/tasks");

        await tasks.RunAsync($"{nameof(WithoutTaskManagement_ScheduleCallIsDisabled)}_TasksPage", async page =>
        {
            // The redirect waits for the storefront to boot and the hub plugin to register its routes.
            await Expect(page).ToHaveURLAsync(new Regex("/company/dashboard$"), new() { Timeout = 60_000 });
            await Expect(page.GetByText("Top sellers", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Tasks & due dates", new() { Exact = true })).ToHaveCountAsync(0);
        });
    }

    private Task<StorefrontSession> OpenAsync(string path)
        => env.OpenAsync(env.RepUserId, path, WithoutTaskManagementEnvironment.NorthwindId);
}
