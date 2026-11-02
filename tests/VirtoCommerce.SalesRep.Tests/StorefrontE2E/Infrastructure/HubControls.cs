using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The hub's task controls, found by role and label as a rep finds them, and the steps the task groups repeat. Dates
/// are typed and read in en-US, the browser's locale.
/// </summary>
internal static class HubControls
{
    /// <summary>
    /// Routes Google Calendar to a stand-in page, so a call's Google tab never leaves the machine. For OpenAsync's
    /// beforeLoad: added once the page was up, while the hub plugin still loaded, the route left the page blank twice.
    /// </summary>
    public static Task StandInForGoogleAsync(IBrowserContext context)
        => context.RouteAsync("https://calendar.google.com/**", route => route.FulfillAsync(new() { ContentType = "text/html", Body = "<title>Google Calendar</title>" }));

    public static ILocator Dialog(IPage page, string title) => page.GetByRole(AriaRole.Dialog, new() { Name = title });

    /// <summary>A select by its label: VcSelect renders a combobox that shows the selected item's text.</summary>
    public static ILocator Field(ILocator dialog, string label) => dialog.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

    /// <summary>The date picker's input: a combobox too, as it opens a calendar.</summary>
    public static ILocator DueDate(ILocator dialog) => Field(dialog, "Due date");

    public static ILocator SaveButton(ILocator dialog) => dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true });

    public static ILocator CancelButton(ILocator dialog) => dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true });

    /// <summary>The "Add to calendar" checkboxes, a group named by its label.</summary>
    public static ILocator Calendars(ILocator dialog) => dialog.GetByRole(AriaRole.Group, new() { Name = "Add to calendar", Exact = true });

    /// <summary>A select's option; VcSelect lists them outside the dialog.</summary>
    public static ILocator Option(IPage page, string text) => page.GetByRole(AriaRole.Option, new() { NameRegex = Exactly(text) });

    /// <summary>The task list's row for a task, found by its edit button.</summary>
    public static ILocator Row(IPage page, string name)
        => page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Button, new() { Name = $"Edit “{name}”", Exact = true }) });

    /// <summary>Every task row on the page.</summary>
    public static ILocator Rows(IPage page)
        => page.GetByRole(AriaRole.Row).Filter(new() { Has = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Edit “") }) });

    /// <summary>A row's completion action ("Mark as complete" or "Reopen"); a canceled task has none.</summary>
    public static ILocator RowAction(IPage page, string name)
        => Row(page, name).GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"^(Mark as complete|Reopen): “{Regex.Escape(name)}”$") });

    /// <summary>A tab of the tasks page (Today, All, Upcoming, Overdue, Completed), named by its label and badge.</summary>
    public static ILocator Tab(IPage page, string label) => page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($@"^{Regex.Escape(label)} \d+$") });

    /// <summary>A tab whose badge shows <paramref name="count"/>.</summary>
    public static ILocator Tab(IPage page, string label, int count) => page.GetByRole(AriaRole.Button, new() { Name = $"{label} {count}", Exact = true });

    /// <summary>The number a tab's badge shows.</summary>
    public static async Task<int> BadgeAsync(IPage page, string label)
    {
        var text = await Tab(page, label).InnerTextAsync(new() { Timeout = 60_000 });
        return int.Parse(Regex.Match(text, @"(\d+)\s*$").Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>The list header's count, announced with the list's title: "1 task", "3 tasks".</summary>
    public static ILocator HeaderCount(IPage page, int count)
        => page.GetByRole(AriaRole.Status).GetByText(count == 1 ? "1 task" : $"{count} tasks", new() { Exact = true });

    public static async Task<ILocator> NewTaskAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New task", Exact = true }).ClickAsync(new() { Timeout = 60_000 });
        return Dialog(page, "New task");
    }

    /// <summary>A quick action on a customer's page, by its label.</summary>
    public static ILocator QuickAction(IPage page, string label) => page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true });

    /// <summary>"Schedule call" on a customer's page.</summary>
    public static async Task<ILocator> ScheduleCallAsync(IPage page)
    {
        await QuickAction(page, "Schedule call").ClickAsync(new() { Timeout = 60_000 });
        return Dialog(page, "Schedule call");
    }

    /// <summary>The draft "Schedule call" opens: a call about the customer, its day left to pick, at 9:00 AM, Normal.</summary>
    public static async Task ExpectNewCallDraftAsync(ILocator dialog, string customer)
    {
        await Expect(dialog.GetByLabel("Title")).ToHaveValueAsync("Call");
        await Expect(Field(dialog, "Customer")).ToHaveValueAsync(customer);
        await Expect(DueDate(dialog)).ToHaveValueAsync(string.Empty);
        await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("9:00 AM"));
        await Expect(Field(dialog, "Priority")).ToHaveValueAsync("Normal");
    }

    /// <summary>Both calendars offered, neither ticked.</summary>
    public static async Task ExpectCalendarsUntickedAsync(ILocator dialog)
    {
        await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(2);
        await Expect(Calendars(dialog).GetByRole(AriaRole.Checkbox, new() { Checked = true })).ToHaveCountAsync(0);
    }

    public static async Task<ILocator> EditAsync(IPage page, string name, string title)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = $"Edit “{name}”", Exact = true }).ClickAsync();
        return Dialog(page, title);
    }

    /// <summary>The "All" tab: the page opens on today.</summary>
    public static Task ShowAllAsync(IPage page) => Tab(page, "All").ClickAsync(new() { Timeout = 60_000 });

    public static async Task PickAsync(IPage page, ILocator field, string option)
    {
        await field.ClickAsync();
        await Option(page, option).ClickAsync();
    }

    /// <summary>Picks a customer the way a rep does, by typing the start of its name.</summary>
    public static async Task PickCustomerAsync(IPage page, ILocator dialog, string name)
    {
        await Field(dialog, "Customer").ClickAsync();
        await Field(dialog, "Customer").FillAsync(name[..3]);
        await Option(page, name).ClickAsync();
    }

    /// <summary>Types a day into the date picker; Enter commits it without moving the focus on.</summary>
    public static async Task TypeDayAsync(ILocator dialog, DateTime day)
    {
        await DueDate(dialog).FillAsync(Typed(day));
        await DueDate(dialog).PressAsync("Enter");
    }

    /// <summary>Ticks a checkbox by its label, as a rep does: VcCheckbox draws its box over the real input.</summary>
    public static async Task TickAsync(ILocator dialog, string label)
    {
        await dialog.GetByText(label, new() { Exact = true }).ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true })).ToBeCheckedAsync();
    }

    /// <summary>Saves, and waits for the dialog to close, which it does once the save went through.</summary>
    public static async Task SaveAsync(IPage page, ILocator dialog)
    {
        await SaveButton(dialog).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(SavedToast(page)).ToBeVisibleAsync();
    }

    /// <summary>
    /// The success toast of a save. Exact: "Task saved, but it could not be added to the calendar" is a warning, not
    /// this. Last: the toast it replaces stays in the page for its leave animation.
    /// </summary>
    public static ILocator SavedToast(IPage page) => page.GetByText("Task saved", new() { Exact = true }).Last;

    /// <summary>
    /// Picks a day in the page's Due dates calendar, paging from the month it shows: today's when the page opens, the
    /// last picked day's after that.
    /// </summary>
    public static async Task PickDueDayAsync(IPage page, DateTime day, DateTime shown)
    {
        var calendar = page.GetByRole(AriaRole.Complementary, new() { Name = "Due dates", Exact = true });
        var months = (day.Year - shown.Year) * 12 + day.Month - shown.Month;
        var pager = calendar.GetByRole(AriaRole.Button, new() { Name = months > 0 ? "Next month" : "Previous month", Exact = true });
        for (var i = 0; i < Math.Abs(months); i++)
        {
            await pager.ClickAsync();
        }

        await calendar.GetByRole(AriaRole.Button, new() { Name = day.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture), Exact = true }).ClickAsync();
    }

    /// <summary>A day as the date picker takes it typed.</summary>
    public static string Typed(DateTime day) => day.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    /// <summary>A day as the task list prints it (the storefront's "short" date).</summary>
    public static string Listed(DateTime day) => day.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    /// <summary>A time of day as the time select and the task list print it.</summary>
    public static string TimeOfDay(DateTime time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>
    /// The text exactly, for an input's value or an option's name. Intl puts a narrow no-break space before AM/PM, so
    /// every space matches any; a text assertion given a string collapses whitespace itself, that space included.
    /// </summary>
    public static Regex Exactly(string text) => new($"^{Regex.Escape(text).Replace(@"\ ", @"\s")}$");
}
