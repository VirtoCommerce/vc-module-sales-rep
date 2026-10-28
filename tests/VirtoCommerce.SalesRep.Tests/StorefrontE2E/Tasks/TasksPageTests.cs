using System.Text.RegularExpressions;
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
/// edited, a delete, completion across the statuses, and the filters with their chips. Each test checks the page and
/// the row. The edits are Tom's; a test that adds a task signs in as a rep of its own.
/// </summary>
[Collection(TasksCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class TasksPageTests(TasksEnvironment env)
{
    private const string PlannedVisit = "Plan the next visit";

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

            // Under no status. Where the list could still be the previous one, a task of its own shows it loaded.
            await PickFilterAsync(page, "Status", "Upcoming");
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);
            await PickFilterAsync(page, "Status", "Overdue");
            await Expect(Row(page, TasksEnvironment.OverdueTask)).ToBeVisibleAsync();
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);
            await PickFilterAsync(page, "Status", "Completed");
            await Expect(Row(page, TasksEnvironment.CompletedTask)).ToBeVisibleAsync();
            await Expect(Row(page, TasksEnvironment.CanceledTask)).ToHaveCountAsync(0);

            // A completed task stays completed through an edit too.
            await RemoveFilterButton(page, "Completed").ClickAsync();
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
    public async Task DeleteTask_RemovesTheRow()
    {
        const string name = "Task to delete";
        string id = null;

        var rep = await NewRepAsync();
        await using var session = await OpenAsync(rep.UserId);

        await session.RunAsync(nameof(DeleteTask_RemovesTheRow), async page =>
        {
            // Every date: the rep's one task, then the new one beside it.
            await ShowAllAsync(page);
            await Expect(HeaderCount(page, 1)).ToBeVisibleAsync(new() { Timeout = 60_000 });

            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await TypeDayAsync(dialog, NewYork.Today.AddDays(1));
            await SaveAsync(page, dialog);
            await Expect(Row(page, name)).ToBeVisibleAsync();
            await Expect(HeaderCount(page, 2)).ToBeVisibleAsync();
            id = (await env.WaitForTaskAsync(x => x.Name == name)).Id;

            dialog = await EditAsync(page, name, "Edit task");
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Dialog(page, "Delete task").GetByRole(AriaRole.Button, new() { Name = "OK", Exact = true }).ClickAsync();

            await Expect(page.GetByText("Task deleted", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(Row(page, name)).ToHaveCountAsync(0);
            await Expect(HeaderCount(page, 1)).ToBeVisibleAsync();
        });

        // Task management deletes the row itself, not a flag on it.
        (await env.CountTasksAsync(x => x.Id == id)).Should().Be(0);
    }

    [Fact(Timeout = 300_000)]
    public async Task CompleteThenReopen_MovesTheTaskBetweenStatuses()
    {
        const string name = "Task to complete";

        var rep = await NewRepAsync();
        await using var session = await OpenAsync(rep.UserId);

        await session.RunAsync(nameof(CompleteThenReopen_MovesTheTaskBetweenStatuses), async page =>
        {
            // Saved onto another day, the list follows it there.
            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await TypeDayAsync(dialog, NewYork.Today.AddDays(3));
            await SaveAsync(page, dialog);
            await ExpectStatusAsync(page, name, "Upcoming", "Mark as complete");

            await RowAction(page, name).ClickAsync();
            await ExpectStatusAsync(page, name, "Completed", "Reopen");
            (await env.WaitForTaskAsync(x => x.Name == name && x.Completed == true)).Should().NotBeNull();

            // Under Completed while it is, and out of it once reopened.
            await PickFilterAsync(page, "Status", "Completed");
            await Expect(Row(page, name)).ToBeVisibleAsync();
            await RowAction(page, name).ClickAsync();
            await Expect(Row(page, name)).ToHaveCountAsync(0);

            await RemoveFilterButton(page, "Completed").ClickAsync();
            await ExpectStatusAsync(page, name, "Upcoming", "Mark as complete");
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.IsActive.Should().BeTrue();
        task.Completed.Should().NotBe(true);
    }

    // VCST-6157: the customer and the priority as columns, and every filter narrowing the list, its count and the
    // calendar's dots together. Each filter and the day is a chip; Reset takes them all.
    [Fact(Timeout = 300_000)]
    public async Task Filters_NarrowTheListAndTheDots_AndResetRestoresBoth()
    {
        const string northwindCall = "Call about the spring order";
        const string contosoQuote = "Send the price list";
        var tomorrow = NewYork.Today.AddDays(1);

        var rep = await NewRepAsync(TasksEnvironment.NorthwindId, TasksEnvironment.ContosoId);
        await env.AddTaskAsync(rep, northwindCall, NewYork.At(tomorrow, 12), TasksEnvironment.NorthwindId, "Highest");
        await env.AddTaskAsync(rep, contosoQuote, NewYork.At(tomorrow, 14), TasksEnvironment.ContosoId, "Low");
        await using var session = await OpenAsync(rep.UserId);

        await session.RunAsync(nameof(Filters_NarrowTheListAndTheDots_AndResetRestoresBoth), async page =>
        {
            await ShowAllAsync(page);
            await Expect(Rows(page)).ToHaveCountAsync(3, new() { Timeout = 60_000 });
            await Expect(CustomerCell(page, northwindCall)).ToHaveTextAsync(TasksEnvironment.NorthwindName);
            await Expect(Row(page, northwindCall)).ToContainTextAsync("Highest");
            await Expect(CustomerCell(page, PlannedVisit)).ToHaveTextAsync("—");
            await Expect(DueDay(page, tomorrow)).ToHaveAccessibleDescriptionAsync(new Regex(@"^2 tasks\."));

            await PickFilterAsync(page, "Customers", TasksEnvironment.ContosoName);
            await Expect(Rows(page)).ToHaveCountAsync(1);
            await Expect(Row(page, contosoQuote)).ToBeVisibleAsync();
            await Expect(HeaderCount(page, 1)).ToBeVisibleAsync();
            await Expect(DueDay(page, tomorrow)).ToHaveAccessibleDescriptionAsync(new Regex(@"^1 task\."));

            // The priorities intersect with the customers: Contoso's one task is Low.
            await PickFilterAsync(page, "Priorities", "Highest");
            await Expect(Rows(page)).ToHaveCountAsync(0);
            await Expect(page.GetByText("No tasks match the filters", new() { Exact = true })).ToBeVisibleAsync();

            // A chip removes its own filter alone.
            await RemoveFilterButton(page, TasksEnvironment.ContosoName).ClickAsync();
            await Expect(Rows(page)).ToHaveCountAsync(1);
            await Expect(Row(page, northwindCall)).ToBeVisibleAsync();

            // The day picked on the calendar is a chip too, and Reset takes every chip, the day included.
            await PickDueDayAsync(page, tomorrow, NewYork.Today);
            await Expect(RemoveFilterButton(page, $"Due {Listed(tomorrow)}")).ToBeVisibleAsync();
            await ResetFiltersAsync(page);
            await Expect(Rows(page)).ToHaveCountAsync(3);
            await Expect(RemoveFilterButton(page, $"Due {Listed(tomorrow)}")).ToHaveCountAsync(0);

            await SearchAsync(page, "price list");
            await Expect(Rows(page)).ToHaveCountAsync(1);
            await Expect(Row(page, contosoQuote)).ToBeVisibleAsync();
        });

        session.GraphQlErrors.Should().BeEmpty();
    }

    private Task<StorefrontSession> OpenAsync() => OpenAsync(env.RepUserId);

    private Task<StorefrontSession> OpenAsync(string userId) => env.OpenAsync(userId, "/company/tasks", TasksEnvironment.NorthwindId);

    /// <summary>A rep of the test's own, serving Northwind unless told otherwise, with one task of no customer already.</summary>
    private async Task<SalesRepDetails> NewRepAsync(params string[] customerIds)
    {
        var rep = await env.NewRepAsync(customerIds.Length > 0 ? customerIds : [TasksEnvironment.NorthwindId]);
        await env.AddTaskAsync(rep, PlannedVisit, NewYork.At(NewYork.Today.AddDays(10), 12));
        return rep;
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
