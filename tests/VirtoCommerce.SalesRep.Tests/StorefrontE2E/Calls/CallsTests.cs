using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>
/// Calls in the hub: booking one from a customer's page or from the task form, adding a new one to the rep's calendars,
/// editing calls and tasks, what the task list shows of a call, and a call about a customer the rep no longer serves.
/// Each test checks the page and the row.
/// </summary>
[Collection(CallsCollection.Name)]
[Trait("Category", "StorefrontE2E")]
public sealed class CallsTests(CallsEnvironment env)
{
    private const string Call = SalesRepModuleConstants.Tasks.CallType;
    private const string CustomerLink = SalesRepModuleConstants.Tasks.CustomerObjectType;

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task ScheduleCall_FromCustomerProfile_CreatesACallForThatCustomer()
    {
        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.NorthwindId}");

        await session.RunAsync(nameof(ScheduleCall_FromCustomerProfile_CreatesACallForThatCustomer), async page =>
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true }).ClickAsync(new() { Timeout = 60_000 });

            // A call about this customer, its day left to pick.
            var dialog = Dialog(page, "Schedule call");
            await Expect(dialog.GetByLabel("Title")).ToHaveValueAsync("Call");
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.NorthwindName);
            await Expect(DueDate(dialog)).ToHaveValueAsync(string.Empty);
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("9:00 AM"));
            await Expect(Field(dialog, "Priority")).ToHaveValueAsync("Normal");
            await Expect(Field(dialog, "Type")).ToHaveCountAsync(0);
            await Expect(SaveButton(dialog)).ToBeDisabledAsync();

            await TypeTomorrowAsync(dialog);
            await PickAsync(page, Field(dialog, "Time"), "2:30 PM");
            await SaveAsync(page, dialog);
        });

        var task = await env.WaitForTaskAsync(x => x.Name == "Call" && x.ObjectId == CallsEnvironment.NorthwindId);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.ObjectType.Should().Be(CustomerLink);
        task.DueDate.Should().Be(CallsEnvironment.At(14, 30));
        task.Priority.Should().Be("Normal");
        task.ResponsibleId.Should().Be(env.RepMemberId);
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task ScheduleCall_TickingBothCalendars_OpensGoogleAndDownloadsTheEvent()
    {
        string googleUrl = null;
        string icsFileName = null;
        string ics = null;

        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.ContosoId}");

        await session.RunAsync(nameof(ScheduleCall_TickingBothCalendars_OpensGoogleAndDownloadsTheEvent), async page =>
        {
            // Google itself is never reached: its tab gets a stand-in page.
            await page.Context.RouteAsync("https://calendar.google.com/**", route => route.FulfillAsync(new() { ContentType = "text/html", Body = "<title>Google Calendar</title>" }));

            await page.GetByRole(AriaRole.Button, new() { Name = "Schedule call", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, "Schedule call");
            await TypeTomorrowAsync(dialog);
            await PickAsync(page, Field(dialog, "Time"), "11:45 AM");

            // Both offered unticked, every time.
            await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(2);
            await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox, new() { Checked = true })).ToHaveCountAsync(0);

            await TickAsync(dialog, "Google Calendar");
            await TickAsync(dialog, "Apple Calendar");

            var popup = page.WaitForPopupAsync();
            var download = page.WaitForDownloadAsync();
            await SaveAsync(page, dialog);

            // The tab opens blank on the click, and gets Google's page once the call is saved.
            var google = await popup;
            await Expect(google).ToHaveURLAsync(new Regex("^https://calendar\\.google\\.com/"));
            googleUrl = google.Url;

            var file = await download;
            icsFileName = file.SuggestedFilename;
            ics = await File.ReadAllTextAsync(await file.PathAsync());

            await Expect(dialog).ToHaveCountAsync(0);
        });

        var task = await env.WaitForTaskAsync(x => x.ObjectId == CallsEnvironment.ContosoId && x.DueDate == CallsEnvironment.At(11, 45));
        task.Should().NotBeNull();

        var start = Stamp(CallsEnvironment.At(11, 45));
        var end = Stamp(CallsEnvironment.At(12, 15));

        var query = QueryHelpers.ParseQuery(new Uri(googleUrl).Query);
        query["action"].ToString().Should().Be("TEMPLATE");
        query["text"].ToString().Should().Be($"Call — {CallsEnvironment.ContosoName}");
        query["dates"].ToString().Should().Be($"{start}/{end}");

        icsFileName.Should().Be("call.ics");
        ics.Should().Contain($"\r\nUID:{task.Id}\r\n")
            .And.Contain($"\r\nDTSTART:{start}\r\n")
            .And.Contain($"\r\nDTEND:{end}\r\n")
            .And.Contain($"\r\nSUMMARY:Call — {CallsEnvironment.ContosoName}\r\n");
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task NewTask_PickingCall_AddsCustomerAndTime_AndSavesACall()
    {
        const string name = "Call about the spring order";

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewTask_PickingCall_AddsCustomerAndTime_AndSavesACall), async page =>
        {
            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);

            // A plain task until it becomes a call: no customer, no time of day.
            await Expect(Field(dialog, "Customer")).ToHaveCountAsync(0);
            await Expect(Field(dialog, "Time")).ToHaveCountAsync(0);

            await PickAsync(page, Field(dialog, "Type"), Call);
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("9:00 AM"));

            // Only the rep's own customers are offered: not the one they stopped serving, not one they never did.
            await Field(dialog, "Customer").ClickAsync();
            await Expect(Option(page, CallsEnvironment.NorthwindName)).ToBeVisibleAsync();
            await Expect(Option(page, CallsEnvironment.ContosoName)).ToBeVisibleAsync();
            await Expect(Option(page, CallsEnvironment.FabrikamName)).ToHaveCountAsync(0);
            await Expect(Option(page, CallsEnvironment.AdventureWorksName)).ToHaveCountAsync(0);
            await Field(dialog, "Customer").FillAsync("Con");
            await Option(page, CallsEnvironment.ContosoName).ClickAsync();

            await TypeTomorrowAsync(dialog);
            await SaveAsync(page, dialog);
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.ObjectType.Should().Be(CustomerLink);
        task.ObjectId.Should().Be(CallsEnvironment.ContosoId);
        task.DueDate.Should().Be(CallsEnvironment.At(9));
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task NewTask_LeavingCall_SavesAPlainTask()
    {
        const string name = "Ask about the catalog";

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewTask_LeavingCall_SavesAPlainTask), async page =>
        {
            var dialog = await NewTaskAsync(page);
            await dialog.GetByLabel("Title").FillAsync(name);
            await PickAsync(page, Field(dialog, "Type"), Call);
            await PickCustomerAsync(page, dialog, CallsEnvironment.NorthwindName);

            // Another type takes the call's fields away again, the customer picked there included.
            await PickAsync(page, Field(dialog, "Type"), "Other");
            await Expect(Field(dialog, "Customer")).ToHaveCountAsync(0);
            await Expect(Field(dialog, "Time")).ToHaveCountAsync(0);

            await TypeTomorrowAsync(dialog);
            await SaveAsync(page, dialog);
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.Type.Should().Be("Other");
        task.ObjectType.Should().BeNull();
        task.ObjectId.Should().BeNull();
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task TasksList_ShowsACallsTimeAndCustomer()
    {
        var day = CallsEnvironment.ListedTomorrow;

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(TasksList_ShowsACallsTimeAndCustomer), async page =>
        {
            await ShowAllAsync(page);

            await Expect(Row(page, CallsEnvironment.ListedCall))
                .ToContainTextAsync(Containing($"Due {day}, 10:15 AM · {CallsEnvironment.ContosoName} · Call"));
            await Expect(Row(page, CallsEnvironment.ListedCallWithoutCustomer))
                .ToContainTextAsync(Containing($"Due {day}, 11:00 AM · Customer not assigned · Call"));

            // A plain task is due on a day: no time, and no customer line.
            await Expect(Row(page, CallsEnvironment.ListedTask)).ToContainTextAsync(Containing($"Due {day} · Other"));
        });
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task EditCall_ChangesTimeCustomerAndPriority()
    {
        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(EditCall_ChangesTimeCustomerAndPriority), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, CallsEnvironment.CallToEdit, "Edit call");

            // A call stays a call: no type to change; its customer and time of day are shown. No calendars either: an
            // event added when the call was created cannot be updated from here.
            await Expect(Field(dialog, "Type")).ToHaveCountAsync(0);
            await Expect(Calendars(dialog)).ToHaveCountAsync(0);
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.ContosoName);
            await Expect(DueDate(dialog)).ToHaveValueAsync(CallsEnvironment.TypedTomorrow);
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("10:00 AM"));

            await PickAsync(page, Field(dialog, "Time"), "11:30 AM");
            await PickCustomerAsync(page, dialog, CallsEnvironment.NorthwindName);
            await PickAsync(page, Field(dialog, "Priority"), "High");
            await SaveAsync(page, dialog);

            await Expect(Row(page, CallsEnvironment.CallToEdit))
                .ToContainTextAsync(Containing($"Due {CallsEnvironment.ListedTomorrow}, 11:30 AM · {CallsEnvironment.NorthwindName} · Call"));
        });

        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallToEdit && x.ObjectId == CallsEnvironment.NorthwindId);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.ObjectType.Should().Be(CustomerLink);
        task.DueDate.Should().Be(CallsEnvironment.At(11, 30));
        task.Priority.Should().Be("High");
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task EditCall_ClearingTheCustomer_UnlinksIt()
    {
        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(EditCall_ClearingTheCustomer_UnlinksIt), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, CallsEnvironment.CallToUnlink, "Edit call");
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(CallsEnvironment.NorthwindName);

            await dialog.Locator(".sales-rep-task-customer-select").GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true }).ClickAsync();
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(string.Empty);
            await SaveAsync(page, dialog);

            await Expect(Row(page, CallsEnvironment.CallToUnlink)).ToContainTextAsync(Containing("· Customer not assigned · Call"));
        });

        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallToUnlink && x.ObjectId == null);
        task.Should().NotBeNull();
        task.Type.Should().Be(Call);
        task.ObjectType.Should().BeNull();
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task EditTask_PickingCall_TurnsItIntoACall()
    {
        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(EditTask_PickingCall_TurnsItIntoACall), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, CallsEnvironment.TaskToTurnIntoCall, "Edit task");
            await Expect(Field(dialog, "Type")).ToHaveValueAsync("Other");
            await Expect(Field(dialog, "Customer")).ToHaveCountAsync(0);

            await PickAsync(page, Field(dialog, "Type"), Call);
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("9:00 AM"));
            await PickCustomerAsync(page, dialog, CallsEnvironment.ContosoName);
            await PickAsync(page, Field(dialog, "Time"), "4:00 PM");
            await SaveAsync(page, dialog);

            // Now a call, it opens as one.
            await Expect(await EditAsync(page, CallsEnvironment.TaskToTurnIntoCall, "Edit call")).ToBeVisibleAsync();
        });

        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.TaskToTurnIntoCall && x.Type == Call);
        task.Should().NotBeNull();
        task.ObjectType.Should().Be(CustomerLink);
        task.ObjectId.Should().Be(CallsEnvironment.ContosoId);
        task.DueDate.Should().Be(CallsEnvironment.At(16));
    }

    [Fact(Timeout = 300_000, Skip = StorefrontAvailability.SkipReason, SkipUnless = nameof(StorefrontAvailability.IsAvailable), SkipType = typeof(StorefrontAvailability))]
    public async Task FormerCustomer_IsHidden_AndAnEditKeepsTheLink()
    {
        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(FormerCustomer_IsHidden_AndAnEditKeepsTheLink), async page =>
        {
            await ShowAllAsync(page);

            // The call is still the rep's; the customer no longer is, so the list does not name it.
            await Expect(Row(page, CallsEnvironment.CallAboutFormerCustomer))
                .ToContainTextAsync(Containing($"Due {CallsEnvironment.ListedTomorrow}, 3:00 PM · Customer not assigned · Call"));
            await Expect(page.GetByRole(AriaRole.Main)).Not.ToContainTextAsync(CallsEnvironment.FabrikamName);

            var dialog = await EditAsync(page, CallsEnvironment.CallAboutFormerCustomer, "Edit call");
            await Expect(Field(dialog, "Customer")).ToHaveValueAsync(string.Empty);
            await Field(dialog, "Customer").ClickAsync();
            await Expect(Option(page, CallsEnvironment.NorthwindName)).ToBeVisibleAsync();
            await Expect(Option(page, CallsEnvironment.FabrikamName)).ToHaveCountAsync(0);
            await Field(dialog, "Customer").PressAsync("Escape");

            await PickAsync(page, Field(dialog, "Time"), "4:00 PM");
            await SaveAsync(page, dialog);
        });

        // Rescheduled, and the customer the rep can no longer see is still the one the call is about.
        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallAboutFormerCustomer && x.DueDate == CallsEnvironment.At(16));
        task.Should().NotBeNull();
        task.ObjectType.Should().Be(CustomerLink);
        task.ObjectId.Should().Be(CallsEnvironment.FabrikamId);
    }

    private Task<StorefrontSession> OpenAsync(string path) => env.OpenAsync(env.RepUserId, path, CallsEnvironment.NorthwindId);

    private static ILocator Dialog(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    /// <summary>A select by its label: VcSelect renders a combobox that shows the selected item's text.</summary>
    private static ILocator Field(ILocator dialog, string label) => dialog.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

    /// <summary>The date picker's input: a combobox too, as it opens a calendar.</summary>
    private static ILocator DueDate(ILocator dialog) => Field(dialog, "Due date");

    private static ILocator SaveButton(ILocator dialog) => dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });

    /// <summary>The "Add to calendar" checkboxes, a group named by its label.</summary>
    private static ILocator Calendars(ILocator dialog) => dialog.GetByRole(AriaRole.Group, new() { Name = "Add to calendar", Exact = true });

    /// <summary>A select's option; VcSelect lists them outside the dialog.</summary>
    private static ILocator Option(IPage page, string text) => page.GetByRole(AriaRole.Option, new() { NameRegex = Exactly(text) });

    /// <summary>The task list's row for a task, found by its edit button.</summary>
    private static ILocator Row(IPage page, string name)
        => page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Button, new() { Name = $"Edit “{name}”", Exact = true }) });

    private static async Task<ILocator> NewTaskAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New task", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
        return Dialog(page, "New task");
    }

    private static async Task<ILocator> EditAsync(IPage page, string name, string title)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = $"Edit “{name}”", Exact = true }).ClickAsync();
        return Dialog(page, title);
    }

    /// <summary>The "All" tab: the seeded tasks are due tomorrow, and the page opens on today.</summary>
    private static Task ShowAllAsync(IPage page)
        => page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex(@"^All \d+$") }).ClickAsync(new() { Timeout = 60_000 });

    private static async Task PickAsync(IPage page, ILocator field, string option)
    {
        await field.ClickAsync();
        await Option(page, option).ClickAsync();
    }

    /// <summary>Picks a customer the way a rep does, by typing the start of its name.</summary>
    private static async Task PickCustomerAsync(IPage page, ILocator dialog, string name)
    {
        await Field(dialog, "Customer").ClickAsync();
        await Field(dialog, "Customer").FillAsync(name[..3]);
        await Option(page, name).ClickAsync();
    }

    /// <summary>Types tomorrow into the date picker; Enter commits it without moving the focus on.</summary>
    private static async Task TypeTomorrowAsync(ILocator dialog)
    {
        await DueDate(dialog).FillAsync(CallsEnvironment.TypedTomorrow);
        await DueDate(dialog).PressAsync("Enter");
    }

    /// <summary>Ticks a checkbox by its label, as a rep does: VcCheckbox draws its box over the real input.</summary>
    private static async Task TickAsync(ILocator dialog, string label)
    {
        await dialog.GetByText(label, new() { Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true })).ToBeCheckedAsync();
    }

    private static async Task SaveAsync(IPage page, ILocator dialog)
    {
        await SaveButton(dialog).ClickAsync();
        // Exact: "Task saved, but it could not be added to the calendar" is a warning, not this.
        await Expect(page.GetByText("Task saved", new() { Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>The text exactly. Intl puts a narrow no-break space before AM/PM, so every space matches any.</summary>
    private static Regex Exactly(string text) => new($"^{Spaced(text)}$");

    /// <summary>The text anywhere, spaces matched as in <see cref="Exactly"/>.</summary>
    private static Regex Containing(string text) => new(Spaced(text));

    private static string Spaced(string text) => Regex.Escape(text).Replace(@"\ ", @"\s");

    /// <summary>An instant as a calendar event carries it: UTC, to the second.</summary>
    private static string Stamp(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
}
