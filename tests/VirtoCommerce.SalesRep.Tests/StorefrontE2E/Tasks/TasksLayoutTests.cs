using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>
/// The tasks page's layout at each of QA's widths, with the stress content of <see cref="TasksLayoutEnvironment"/>: as
/// it opens, under every filter, with the Customers filter open, and with a task's edit popup open. A test fails on a
/// sideways scroll or on text cut off without an ellipsis, and lists every width and element that did it. These tests
/// check what fits, not what works; the tasks group checks that.
/// </summary>
[Collection(TasksLayoutCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
[Trait("Kind", "Layout")]
public sealed class TasksLayoutTests(TasksLayoutEnvironment env)
{
    private const string PageRoot = ".sales-rep-tasks-page";

    [Fact(Timeout = 300_000)]
    public async Task DefaultView_FitsAtEveryWidth()
    {
        await using var session = await OpenAsync();

        await session.RunAsync(nameof(DefaultView_FitsAtEveryWidth), async page =>
        {
            await ReadyAsync(page);

            await ExpectToFitAsync(page, nameof(DefaultView_FitsAtEveryWidth), [PageRoot]);
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task EveryFilterPicked_FitsAtEveryWidth()
    {
        await using var session = await OpenAsync();

        await session.RunAsync(nameof(EveryFilterPicked_FitsAtEveryWidth), async page =>
        {
            await ReadyAsync(page);

            // Every date, every customer, a status and every priority: the toolbar and the chip row at their fullest.
            await ShowAllAsync(page);
            foreach (var customer in TasksLayoutEnvironment.CustomerNames)
            {
                await PickFilterAsync(page, "Customers", customer);
            }

            await PickFilterAsync(page, "Status", "Upcoming");
            foreach (var priority in TasksLayoutEnvironment.Priorities)
            {
                await PickFilterAsync(page, "Priorities", priority);
            }

            // The list follows the picks a moment later: the task with no customer leaves it, tomorrow's stay.
            await Expect(Row(page, TasksLayoutEnvironment.NoCustomerTask)).ToHaveCountAsync(0);
            await Expect(Row(page, TasksLayoutEnvironment.TomorrowsTask)).ToBeVisibleAsync();

            await ExpectToFitAsync(page, nameof(EveryFilterPicked_FitsAtEveryWidth), [PageRoot]);
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task OpenCustomersFilter_FitsAtEveryWidth()
    {
        await using var session = await OpenAsync();

        await session.RunAsync(nameof(OpenCustomersFilter_FitsAtEveryWidth), async page =>
        {
            await ReadyAsync(page);
            await OpenCustomersAsync(page);

            // The options list is teleported out of the page, so it is a scope of its own.
            await ExpectToFitAsync(page, nameof(OpenCustomersFilter_FitsAtEveryWidth), [PageRoot, "[role=listbox]"], () => OpenCustomersAsync(page));
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task EditPopup_FitsAtEveryWidth()
    {
        await using var session = await OpenAsync();

        await session.RunAsync(nameof(EditPopup_FitsAtEveryWidth), async page =>
        {
            await ReadyAsync(page);

            // The longest title, customer and notes in one popup.
            var dialog = await EditAsync(page, TasksLayoutEnvironment.LongTitleTask, "Edit task");
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(TasksLayoutEnvironment.LongName);

            await ExpectToFitAsync(page, nameof(EditPopup_FitsAtEveryWidth), ["[role=dialog]"], () => Expect(dialog).ToBeVisibleAsync());
        });
    }

    private Task<StorefrontSession> OpenAsync() => env.OpenAsync(env.RepUserId, "/company/tasks", TasksLayoutEnvironment.LongNameId);

    /// <summary>The page shows today's tasks, in its own font, with nothing moving.</summary>
    private static async Task ReadyAsync(IPage page)
    {
        await Expect(Row(page, TasksLayoutEnvironment.LongTitleTask)).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await LayoutAudit.PrepareAsync(page);
    }

    /// <summary>Opens the Customers filter's list, unless it is open already: a resize may close it.</summary>
    private static async Task OpenCustomersAsync(IPage page)
    {
        if (!await page.GetByRole(AriaRole.Listbox).IsVisibleAsync())
        {
            await Filter(page, "Customers").ClickAsync();
        }

        await Expect(Option(page, TasksLayoutEnvironment.LongName)).ToBeVisibleAsync();
    }

    /// <summary>Fails with every width and element at once: a layout fix is checked against the whole list.</summary>
    private static async Task ExpectToFitAsync(IPage page, string name, IReadOnlyList<string> scopes, Func<Task> restoreAsync = null)
    {
        var problems = await LayoutAudit.SweepAsync(page, name, scopes, restoreAsync);
        if (problems.Count > 0)
        {
            Assert.Fail($"The page does not keep its content inside at every width:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
        }
    }
}
