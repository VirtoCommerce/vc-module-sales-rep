using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>
/// The tasks page around a plain task: an edit that changes only the date, a canceled, a completed and a dateless task
/// edited, a delete, and the tabs' badges against their lists. Each test checks the page and the row. The edits are
/// Tom's; a test that adds a task signs in as a rep of its own.
/// </summary>
[Collection(TasksCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class TasksPageTests(TasksEnvironment env)
{
    private static readonly string[] _tabs = ["Today", "All", "Upcoming", "Overdue", "Completed"];

    [Fact(Timeout = 300_000)]
    public async Task EditTask_ChangingOnlyTheDate_KeepsEverythingElse()
    {
        var day = NewYork.Today.AddDays(2);

        await using var session = await OpenAsync();

        await session.RunAsync(nameof(EditTask_ChangingOnlyTheDate_KeepsEverythingElse), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, TasksEnvironment.TaskToRedate, "Edit task");
            await Expect(Field(dialog, "Type")).ToHaveValueAsync("Other");
            await Expect(Field(dialog, "Priority")).ToHaveValueAsync("High");
            await Expect(dialog.GetByLabel("Notes")).ToHaveValueAsync(TasksEnvironment.TaskToRedateNotes);

            await TypeDayAsync(dialog, day);
            await SaveAsync(page, dialog);

            // Listed under its new day. Today's task goes first: its absence waits for that day's own list.
            await PickDueDayAsync(page, day, NewYork.Today);
            await Expect(Row(page, TasksEnvironment.TodaysTask)).ToHaveCountAsync(0);
            await Expect(Row(page, TasksEnvironment.TaskToRedate)).ToBeVisibleAsync();
        });

        // The update replaces the task: every field the form did not touch has to come back as it was.
        var task = await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.TaskToRedate && x.DueDate == NewYork.At(day + TasksEnvironment.TaskToRedateTime));
        task.Should().NotBeNull();
        task.Type.Should().Be("Other");
        task.Priority.Should().Be("High");
        task.Description.Should().Be(TasksEnvironment.TaskToRedateNotes);
        task.IsActive.Should().BeTrue();
        task.Completed.Should().NotBe(true);
    }

    [Fact(Timeout = 300_000)]
    public async Task EditCanceledTask_StaysCanceled()
    {
        var canceledDay = NewYork.Today.AddDays(6);
        var completedDay = NewYork.Today.AddDays(7);

        await using var session = await OpenAsync();

        await session.RunAsync(nameof(EditCanceledTask_StaysCanceled), async page =>
        {
            await ShowAllAsync(page);
            await ExpectStatusAsync(page, TasksEnvironment.CanceledTask, "Canceled", action: null);

            // Saved unchanged, then given a new date: the update replaces the task, and neither may reopen it.
            await SaveAsync(page, await EditAsync(page, TasksEnvironment.CanceledTask, "Edit task"));
            var unchanged = await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.CanceledTask);
            unchanged.IsActive.Should().BeFalse();
            unchanged.Completed.Should().BeFalse();
            await ExpectStatusAsync(page, TasksEnvironment.CanceledTask, "Canceled", action: null);

            var dialog = await EditAsync(page, TasksEnvironment.CanceledTask, "Edit task");
            await TypeDayAsync(dialog, canceledDay);
            await SaveAsync(page, dialog);
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToContainTextAsync($"Due {Listed(canceledDay)}");
            await ExpectStatusAsync(page, TasksEnvironment.CanceledTask, "Canceled", action: null);

            // In no status tab. Where the tab's list could still be the previous one, a task of its own shows it loaded.
            await Tab(page, "Upcoming").ClickAsync();
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);
            await Tab(page, "Overdue").ClickAsync();
            await Expect(Row(page, TasksEnvironment.OverdueTask)).ToBeVisibleAsync();
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);
            await Tab(page, "Completed").ClickAsync();
            await Expect(Row(page, TasksEnvironment.CompletedTask)).ToBeVisibleAsync();
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);

            // A completed task stays completed through an edit too.
            await ShowAllAsync(page);
            dialog = await EditAsync(page, TasksEnvironment.CompletedTask, "Edit task");
            await TypeDayAsync(dialog, completedDay);
            await SaveAsync(page, dialog);
            await Expect(Row(page, TasksEnvironment.CompletedTask)).ToContainTextAsync($"Due {Listed(completedDay)}");
            await ExpectStatusAsync(page, TasksEnvironment.CompletedTask, "Completed", action: "Reopen");
        });

        var canceled = await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.CanceledTask && x.DueDate == NewYork.At(canceledDay, 12));
        canceled.Should().NotBeNull();
        canceled.IsActive.Should().BeFalse();
        canceled.Completed.Should().BeFalse();

        var completed = await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.CompletedTask && x.DueDate == NewYork.At(completedDay, 12));
        completed.Should().NotBeNull();
        completed.IsActive.Should().BeFalse();
        completed.Completed.Should().BeTrue();
    }

    [Fact(Timeout = 300_000)]
    public async Task DatelessTask_IsNotLost()
    {
        var day = NewYork.Today.AddDays(3);

        await using var session = await OpenAsync();

        await session.RunAsync(nameof(DatelessTask_IsNotLost), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, TasksEnvironment.DatelessTask, "Edit task");
            await Expect(DueDate(dialog)).ToHaveValueAsync(string.Empty);

            await page.Keyboard.PressAsync("Escape");
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(Row(page, TasksEnvironment.DatelessTask)).ToBeVisibleAsync();
            (await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.DatelessTask)).DueDate.Should().BeNull();

            dialog = await EditAsync(page, TasksEnvironment.DatelessTask, "Edit task");
            await TypeDayAsync(dialog, day);
            await SaveAsync(page, dialog);
            await Expect(Row(page, TasksEnvironment.DatelessTask)).ToContainTextAsync($"Due {Listed(day)}");
        });

        session.GraphQlErrors.Should().BeEmpty();

        // No time to keep, so the task starts the day.
        var task = await env.WaitForTaskAsync(x => x.Name == TasksEnvironment.DatelessTask);
        task.DueDate.Should().Be(NewYork.At(day, 0));
    }

    [Fact(Timeout = 300_000)]
    public async Task DeleteTask_RemovesTheRowAndUpdatesTheBadges()
    {
        const string name = "Task to delete";
        string id = null;

        var rep = await NewRepAsync();
        await using var session = await OpenAsync(rep.UserId);

        await session.RunAsync(nameof(DeleteTask_RemovesTheRowAndUpdatesTheBadges), async page =>
        {
            var before = await ReadBadgesAsync(page);

            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await TypeDayAsync(dialog, NewYork.Today.AddDays(1));
            await SaveAsync(page, dialog);
            await Expect(Tab(page, "All", before["All"] + 1)).ToBeVisibleAsync();
            await Expect(Tab(page, "Upcoming", before["Upcoming"] + 1)).ToBeVisibleAsync();
            id = (await env.WaitForTaskAsync(x => x.Name == name)).Id;

            dialog = await EditAsync(page, name, "Edit task");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Dialog(page, "Delete task").GetByRole(AriaRole.Button, new() { Name = "OK", Exact = true }).ClickAsync();

            await Expect(page.GetByText("Task deleted", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(Row(page, name)).ToHaveCountAsync(0);
            await Expect(Tab(page, "All", before["All"])).ToBeVisibleAsync();
            await Expect(Tab(page, "Upcoming", before["Upcoming"])).ToBeVisibleAsync();
        });

        // Task management deletes the row itself, not a flag on it.
        (await env.CountTasksAsync(x => x.Id == id)).Should().Be(0);
    }

    [Fact(Timeout = 300_000)]
    public async Task Badges_EqualTheirLists()
    {
        await using var session = await OpenAsync();

        await session.RunAsync(nameof(Badges_EqualTheirLists), async page =>
        {
            var badges = await ReadBadgesAsync(page);

            // The badges come from one counts request; the header and the rows, from each tab's own list.
            foreach (var tab in _tabs)
            {
                await Tab(page, tab).ClickAsync();
                await Expect(HeaderCount(page, badges[tab])).ToBeVisibleAsync();
                await Expect(Rows(page)).ToHaveCountAsync(badges[tab]);
            }
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task CompleteThenReopen_RestoresTheCounts()
    {
        const string name = "Task to complete";

        var rep = await NewRepAsync();
        await using var session = await OpenAsync(rep.UserId);

        await session.RunAsync(nameof(CompleteThenReopen_RestoresTheCounts), async page =>
        {
            var badges = await ReadBadgesAsync(page);

            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await TypeDayAsync(dialog, NewYork.Today.AddDays(3));
            await SaveAsync(page, dialog);
            await ExpectStatusAsync(page, name, "Upcoming", "Mark as complete");

            // With the new task counted.
            var upcoming = badges["Upcoming"] + 1;
            var completed = badges["Completed"];
            await Expect(Tab(page, "Upcoming", upcoming)).ToBeVisibleAsync();

            await RowAction(page, name).ClickAsync();
            await ExpectStatusAsync(page, name, "Completed", "Reopen");
            await Expect(Tab(page, "Upcoming", upcoming - 1)).ToBeVisibleAsync();
            await Expect(Tab(page, "Completed", completed + 1)).ToBeVisibleAsync();
            (await env.WaitForTaskAsync(x => x.Name == name && x.Completed == true)).Should().NotBeNull();

            await RowAction(page, name).ClickAsync();
            await ExpectStatusAsync(page, name, "Upcoming", "Mark as complete");
            await Expect(Tab(page, "Upcoming", upcoming)).ToBeVisibleAsync();
            await Expect(Tab(page, "Completed", completed)).ToBeVisibleAsync();
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.IsActive.Should().BeTrue();
        task.Completed.Should().NotBe(true);
    }

    private Task<StorefrontSession> OpenAsync() => OpenAsync(env.RepUserId);

    private Task<StorefrontSession> OpenAsync(string userId) => env.OpenAsync(userId, "/company/tasks", TasksEnvironment.NorthwindId);

    /// <summary>A rep of the test's own, with one task already, so that <see cref="ReadBadgesAsync"/> can tell the badges loaded.</summary>
    private async Task<SalesRepDetails> NewRepAsync()
    {
        var rep = await env.NewRepAsync(TasksEnvironment.NorthwindId);
        await env.AddTaskAsync(rep, "Plan the next visit", NewYork.At(NewYork.Today.AddDays(10), 12));
        return rep;
    }

    /// <summary>
    /// Every tab's badge. The badges read 0 until their one request returns, and every rep here has a task, so a
    /// non-zero All says they have.
    /// </summary>
    private static async Task<Dictionary<string, int>> ReadBadgesAsync(IPage page)
    {
        await Expect(Tab(page, "All")).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await Expect(Tab(page, "All", 0)).ToHaveCountAsync(0);

        var badges = new Dictionary<string, int>();
        foreach (var tab in _tabs)
        {
            badges[tab] = await BadgeAsync(page, tab);
        }

        return badges;
    }

    /// <summary>A row's status chip and its one action; null for none.</summary>
    private static async Task ExpectStatusAsync(IPage page, string name, string status, string action)
    {
        await Expect(Row(page, name).GetByText(status, new() { Exact = true })).ToBeVisibleAsync();

        if (action == null)
        {
            await Expect(RowAction(page, name)).ToHaveCountAsync(0);
        }
        else
        {
            await Expect(RowAction(page, name)).ToHaveAccessibleNameAsync($"{action}: “{name}”");
        }
    }
}
