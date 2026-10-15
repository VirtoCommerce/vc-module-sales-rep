using System;
using System.Linq;
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
        await using var session = await env.OpenAsync(env.RepUserId, "/company/tasks");

        await session.RunAsync(nameof(Calendar_CompletingTheSeededTask_PersistsTheCompletion), async page =>
        {
            var complete = page.GetByRole(AriaRole.Button, new() { Name = $"Mark as complete: “{CalendarEnvironment.SeededTaskName}”" });
            await Expect(complete).ToBeVisibleAsync(new() { Timeout = 60_000 });

            await complete.ClickAsync();

            // The row's action turns into its opposite once the mutation has landed.
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = $"Reopen: “{CalendarEnvironment.SeededTaskName}”" })).ToBeVisibleAsync();
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

        await using var session = await env.OpenAsync(env.RepUserId, "/company/tasks");

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
            var task = await env.Backend.RunExclusiveAsync(async context =>
            {
                using var db = context.NewTaskManagementDbContext();
                return await db.Set<WorkTaskEntity>().AsNoTracking().FirstOrDefaultAsync(predicate);
            });

            if (task != null || DateTime.UtcNow > deadline)
            {
                return task;
            }

            await Task.Delay(250);
        }
    }
}
