using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.TaskManagement.Data.Models;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calendar;

[Collection(CalendarCollection.Name)]
[Trait("Category", "StorefrontE2E")]
public sealed class CalendarTests(CalendarEnvironment env)
{
    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task Calendar_CompletingTheSeededTask_PersistsTheCompletion()
    {
        await using var session = await env.OpenAsync(env.RepUserId, "/company/calendar");

        await session.RunAsync(nameof(Calendar_CompletingTheSeededTask_PersistsTheCompletion), async page =>
        {
            var completion = page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex(Regex.Escape(CalendarEnvironment.SeededTaskName)) });
            await Expect(completion).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await Expect(completion).Not.ToBeCheckedAsync();

            // The real input sits hidden behind VcCheckbox's indicator; its container owns the click.
            await completion.Locator("xpath=..").ClickAsync();
            await Expect(completion).ToBeCheckedAsync();
        });

        // The storefront's click went through the real mutation into the real table.
        var task = await WaitForTaskAsync(x => x.Id == env.SeededTaskId && x.Completed == true);
        task.Should().NotBeNull();
        task.Completed.Should().BeTrue();
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task Calendar_CreatingATaskFromTheForm_WritesTheWorkTaskRow()
    {
        const string name = "Send the Q4 price list";

        await using var session = await env.OpenAsync(env.RepUserId, "/company/calendar");

        await session.RunAsync(nameof(Calendar_CreatingATaskFromTheForm_WritesTheWorkTaskRow), async page =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "New task", Exact = true }).ClickAsync(new() { Timeout = 60_000 });

            var modal = page.Locator(".sales-rep-task-modal");
            await modal.GetByLabel("Title").FillAsync(name);
            await modal.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

            await Expect(page.GetByText("Task saved")).ToBeVisibleAsync();
        });

        var task = await WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.ResponsibleId.Should().Be(env.RepMemberId);
        task.Completed.Should().NotBe(true);
    }

    /// <summary>The row, or null after ten seconds: the UI confirms before the request's scope has always completed.</summary>
    private async Task<WorkTaskEntity> WaitForTaskAsync(System.Linq.Expressions.Expression<Func<WorkTaskEntity, bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (true)
        {
            using (var db = env.Context.NewTaskManagementDbContext())
            {
                var task = await db.Set<WorkTaskEntity>().AsNoTracking().FirstOrDefaultAsync(predicate);
                if (task != null || DateTime.UtcNow > deadline)
                {
                    return task;
                }
            }

            await Task.Delay(250);
        }
    }
}
