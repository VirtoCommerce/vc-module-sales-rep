using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calendar;

[Collection(CalendarCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class CalendarTests(CalendarEnvironment env)
{
    [Fact(Timeout = 300_000)]
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
        var task = await env.WaitForTaskAsync(x => x.Id == env.SeededTaskId && x.Completed == true);
        task.Should().NotBeNull();
        task.Completed.Should().BeTrue();
    }

    [Fact(Timeout = 300_000)]
    public async Task Calendar_CreatingATaskFromTheForm_WritesTheWorkTaskRow()
    {
        const string name = "Send the Q4 price list";

        // Like every test that adds a task, this one signs in as a rep of its own.
        var rep = await env.NewRepAsync(CalendarEnvironment.NorthwindId);
        await using var session = await env.OpenAsync(rep.UserId, "/company/tasks");

        await session.RunAsync(nameof(Calendar_CreatingATaskFromTheForm_WritesTheWorkTaskRow), async page =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "New task", Exact = true }).ClickAsync(new() { Timeout = 60_000 });

            var modal = page.Locator(".sales-rep-task-modal");
            await modal.GetByLabel("Title").FillAsync(name);
            await modal.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

            await Expect(page.GetByText("Task saved")).ToBeVisibleAsync();
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.ResponsibleId.Should().Be(rep.Id);
        task.Completed.Should().NotBe(true);
    }
}
