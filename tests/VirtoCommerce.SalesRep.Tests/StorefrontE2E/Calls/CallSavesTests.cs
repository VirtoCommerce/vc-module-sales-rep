using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>
/// Saving a call: without a customer, refused because the customer is no longer the rep's, with the Google tab blocked,
/// double-clicked, and abandoned. Erin's calls, so the task list's first page stays Carl's.
/// </summary>
[Collection(CallsCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class CallSavesTests(CallsEnvironment env)
{
    private const string Call = SalesRepModuleConstants.Tasks.CallType;

    [Fact(Timeout = 300_000)]
    public async Task NewTask_CallWithoutCustomer_SavesAsNotAssigned()
    {
        const string name = "Call the new prospect";

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewTask_CallWithoutCustomer_SavesAsNotAssigned), async page =>
        {
            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await PickAsync(page, Field(dialog, "Type"), Call);
            await TypeDayAsync(dialog, CallsEnvironment.Tomorrow);
            await SaveAsync(page, dialog);

            await ShowAllAsync(page);
            await Expect(Row(page, name)).ToContainTextAsync(Containing($"Due {CallsEnvironment.ListedTomorrow}, 9:00 AM · Customer not assigned · Call"));
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.ObjectType.Should().BeNull();
        task.ObjectId.Should().BeNull();
        task.DueDate.Should().Be(CallsEnvironment.At(9));
    }

    [Fact(Timeout = 300_000)]
    public async Task ScheduleCall_RefusedWithGoogleTicked_ClosesTheTab()
    {
        OrganizationMembership removed = null;

        await using var session = await env.OpenAsync(env.ErinUserId, $"/company/my-customers/{CallsEnvironment.WideWorldId}", CallsEnvironment.NorthwindId, beforeLoad: StandInForGoogleAsync);

        try
        {
            await session.RunAsync(nameof(ScheduleCall_RefusedWithGoogleTicked_ClosesTheTab), async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
                var dialog = Dialog(page, "Schedule call");
                await TickAsync(dialog, "Google Calendar");
                await TypeDayAsync(dialog, CallsEnvironment.Tomorrow);

                // By the time Erin saves, the customer is no longer hers: the call is refused.
                removed = await env.StopServingAsync(env.ErinUserId, CallsEnvironment.WideWorldId);
                var blank = page.WaitForPopupAsync();
                await SaveButton(dialog).ClickAsync();

                // The tab the click opened for Google closes again, and the draft stays to retry.
                await WaitUntilClosedAsync(await blank);
                await Expect(dialog.GetByLabel("Title")).ToHaveValueAsync("Call");
                await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.WideWorldName);
                await Expect(DueDate(dialog)).ToHaveValueAsync(CallsEnvironment.TypedTomorrow);
                await Expect(dialog.GetByRole(AriaRole.Checkbox, new() { Name = "Google Calendar", Exact = true })).ToBeCheckedAsync();
                (await env.CountTasksAsync(x => x.ObjectId == CallsEnvironment.WideWorldId)).Should().Be(0);

                await env.ServeAgainAsync(removed);
                removed = null;

                var google = page.WaitForPopupAsync();
                await SaveAsync(page, dialog);
                await Expect(await google).ToHaveURLAsync(new Regex("^https://calendar\\.google\\.com/"));
            });
        }
        finally
        {
            if (removed != null)
            {
                await env.ServeAgainAsync(removed);
            }
        }

        var task = await env.WaitForTaskAsync(x => x.ObjectId == CallsEnvironment.WideWorldId);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.DueDate.Should().Be(CallsEnvironment.At(9));
    }

    [Fact(Timeout = 300_000)]
    public async Task ScheduleCall_GoogleTabBlocked_SavesAndWarns()
    {
        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.ContosoId}");

        await session.RunAsync(nameof(ScheduleCall_GoogleTabBlocked_SavesAndWarns), async page =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, "Schedule call");
            await TypeDayAsync(dialog, CallsEnvironment.Tomorrow);
            await PickAsync(page, Field(dialog, "Time"), "7:15 AM");
            await TickAsync(dialog, "Google Calendar");

            // As a browser that blocks even the tab a click opens.
            await page.EvaluateAsync("() => { window.open = () => null; }");
            await SaveButton(dialog).ClickAsync();

            await Expect(page.GetByText("Task saved, but it could not be added to the calendar", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Task saved", new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(dialog).ToHaveCountAsync(0);
        });

        var task = await env.WaitForTaskAsync(x => x.ResponsibleId == env.ErinMemberId && x.ObjectId == CallsEnvironment.ContosoId && x.DueDate == CallsEnvironment.At(7, 15));
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
    }

    [Fact(Timeout = 300_000, Skip = "VCST-6455: a refused save shows the generic error toast, and the server's reason is dropped.")]
    public async Task ScheduleCall_Refused_ShowsTheReason()
    {
        OrganizationMembership removed = null;

        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.TailspinId}");

        try
        {
            await session.RunAsync(nameof(ScheduleCall_Refused_ShowsTheReason), async page =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
                var dialog = Dialog(page, "Schedule call");
                await TypeDayAsync(dialog, CallsEnvironment.Tomorrow);

                removed = await env.StopServingAsync(env.ErinUserId, CallsEnvironment.TailspinId);
                await SaveButton(dialog).ClickAsync();

                // The rep is told why. Retrying the same draft gets the same answer, so "try again later" is no reason.
                await Expect(page.GetByText(Containing("Customer not found"))).ToBeVisibleAsync();
                await Expect(page.GetByText("Something went wrong. Please try again later.")).ToHaveCountAsync(0);
                await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.TailspinName);
                await Expect(DueDate(dialog)).ToHaveValueAsync(CallsEnvironment.TypedTomorrow);
            });
        }
        finally
        {
            if (removed != null)
            {
                await env.ServeAgainAsync(removed);
            }
        }

        (await env.CountTasksAsync(x => x.ObjectId == CallsEnvironment.TailspinId)).Should().Be(0);
    }

    [Fact(Timeout = 300_000)]
    public async Task SaveDoubleClicked_CreatesOneCall()
    {
        const string name = "Call about the double click";

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(SaveDoubleClicked_CreatesOneCall), async page =>
        {
            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await PickAsync(page, Field(dialog, "Type"), Call);
            await PickCustomerAsync(page, dialog, CallsEnvironment.ContosoName);
            await TypeDayAsync(dialog, CallsEnvironment.Tomorrow);

            await SaveButton(dialog).DblClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(SavedToast(page)).ToBeVisibleAsync();

            // A second create would trail the first: give it time to land before counting.
            await page.WaitForTimeoutAsync(2_000);
        });

        (await env.CountTasksAsync(x => x.Name == name)).Should().Be(1);
    }

    [Fact(Timeout = 300_000)]
    public async Task ScheduleCall_CancelledAfterEdits_ReopensFresh()
    {
        var before = await env.CountTasksAsync(x => x.ResponsibleId == env.ErinMemberId);

        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.NorthwindId}");

        await session.RunAsync(nameof(ScheduleCall_CancelledAfterEdits_ReopensFresh), async page =>
        {
            var scheduleCall = page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true });
            await scheduleCall.ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, "Schedule call");
            await PickAsync(page, Field(dialog, "Time"), "3:45 PM");
            await PickAsync(page, Field(dialog, "Priority"), "High");
            await dialog.GetByLabel("Notes").FillAsync("Bring the catalog");
            await TickAsync(dialog, "Google Calendar");
            await TickAsync(dialog, "Apple Calendar");
            await CancelButton(dialog).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);

            // Nothing of the abandoned draft carries over.
            await scheduleCall.ClickAsync();
            await Expect(dialog.GetByLabel("Title")).ToHaveValueAsync("Call");
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.NorthwindName);
            await Expect(DueDate(dialog)).ToHaveValueAsync(string.Empty);
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("9:00 AM"));
            await Expect(Field(dialog, "Priority")).ToHaveValueAsync("Normal");
            await Expect(dialog.GetByLabel("Notes")).ToHaveValueAsync(string.Empty);
            await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(2);
            await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox, new() { Checked = true })).ToHaveCountAsync(0);
        });

        (await env.CountTasksAsync(x => x.ResponsibleId == env.ErinMemberId)).Should().Be(before);
    }

    private Task<StorefrontSession> OpenAsync(string path) => env.OpenAsync(env.ErinUserId, path, CallsEnvironment.NorthwindId);

    /// <summary>Waits for a tab to close; one that closed before the wait began counts too.</summary>
    private static async Task WaitUntilClosedAsync(IPage tab)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!tab.IsClosed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        tab.IsClosed.Should().BeTrue("the refused save closes the tab its click opened");
    }
}
