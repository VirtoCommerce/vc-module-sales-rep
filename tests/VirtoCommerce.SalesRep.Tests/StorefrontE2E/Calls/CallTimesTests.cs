using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>
/// A call's time of day: the edges of a day, the nights the clocks change, another time zone, a time off the time
/// select's steps, and the select itself. Signed in as Dana.
/// </summary>
[Collection(CallsCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class CallTimesTests(CallsEnvironment env)
{
    private const string Call = SalesRepModuleConstants.Tasks.CallType;

    [Fact(Timeout = 300_000)]
    public async Task NewCall_AtTheEdgesOfTheDay_StaysOnItsOwnDay()
    {
        const string late = "Late call";
        const string midnight = "Midnight call";
        var tomorrow = CallsEnvironment.Tomorrow;
        var dayAfter = tomorrow.AddDays(1);

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewCall_AtTheEdgesOfTheDay_StaysOnItsOwnDay), async page =>
        {
            await BookCallAsync(page, late, tomorrow, "11:45 PM");
            await BookCallAsync(page, midnight, dayAfter, "12:00 AM");

            await ShowAllAsync(page);
            await Expect(Row(page, late)).ToContainTextAsync(Containing($"Due {Listed(tomorrow)}, 11:45 PM · {CallsEnvironment.ContosoName} · Call"));
            await Expect(Row(page, midnight)).ToContainTextAsync(Containing($"Due {Listed(dayAfter)}, 12:00 AM · {CallsEnvironment.ContosoName} · Call"));

            // Each day lists its own call only. The other call goes first: its absence waits for the day's own list.
            await PickDueDayAsync(page, tomorrow, NewYork.Today);
            await Expect(Row(page, midnight)).ToHaveCountAsync(0);
            await Expect(Row(page, late)).ToBeVisibleAsync();

            await PickDueDayAsync(page, dayAfter, tomorrow);
            await Expect(Row(page, late)).ToHaveCountAsync(0);
            await Expect(Row(page, midnight)).ToBeVisibleAsync();
        });

        (await env.WaitForTaskAsync(x => x.Name == late)).DueDate.Should().Be(NewYork.At(tomorrow, 23, 45));
        (await env.WaitForTaskAsync(x => x.Name == midnight)).DueDate.Should().Be(NewYork.At(dayAfter, 0));
    }

    [Fact(Timeout = 300_000)]
    public async Task NewCall_InTheSkippedHour_MovesForward()
    {
        const string name = "Call in the skipped hour";
        // The time select offers 2:30 AM on the night the clocks go forward too, when it does not exist.
        var day = NewYork.NextDaySkipping(2, 30);

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewCall_InTheSkippedHour_MovesForward), async page =>
        {
            await BookCallAsync(page, name, day, "2:30 AM");

            await ShowAllAsync(page);
            await Expect(Row(page, name)).ToContainTextAsync(Containing($"Due {Listed(day)}, 3:30 AM · {CallsEnvironment.ContosoName} · Call"));
        });

        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.DueDate.Should().Be(NewYork.At(day, 3, 30));
    }

    [Fact(Timeout = 300_000)]
    public async Task NewCall_OnTheRepeatedHour_TakesTheFirstOne()
    {
        const string name = "Call booked on the repeated hour";
        var day = CallsEnvironment.RepeatedHourDay;

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(NewCall_OnTheRepeatedHour_TakesTheFirstOne), async page =>
        {
            await BookCallAsync(page, name, day, "1:30 AM");

            await ShowAllAsync(page);
            await Expect(Row(page, name)).ToContainTextAsync(Containing($"Due {Listed(day)}, 1:30 AM · {CallsEnvironment.ContosoName} · Call"));
        });

        // 1:30 AM happens twice that night. The browser reads a time of day with the offset from before the change, the
        // rule that also moves a skipped 2:30 to 3:30, so the call is the first 1:30, still in daylight time.
        var task = await env.WaitForTaskAsync(x => x.Name == name);
        task.Should().NotBeNull();
        task.DueDate.Should().Be(NewYork.AtFirstPass(day, 1, 30));
    }

    [Fact(Timeout = 300_000)]
    public async Task EditCall_OnTheRepeatedHour_KeepsItsInstant()
    {
        const string notes = "Confirm the agenda";
        var day = CallsEnvironment.RepeatedHourDay;

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(EditCall_OnTheRepeatedHour_KeepsItsInstant), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, CallsEnvironment.CallOnTheRepeatedHour, "Edit call");
            await Expect(DueDate(dialog)).ToHaveValueAsync(Typed(day));
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("1:30 AM"));

            await dialog.GetByLabel("Notes").FillAsync(notes);
            await SaveAsync(page, dialog);
        });

        // Rebuilt from its day and time, the call would move to the first 1:30, an hour earlier.
        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallOnTheRepeatedHour && x.Description == notes);
        task.Should().NotBeNull();
        task.DueDate.Should().Be(CallsEnvironment.SecondOneThirty);
    }

    [Fact(Timeout = 300_000)]
    public async Task Call_SeenFromAnotherTimeZone_ShowsTheSameInstant()
    {
        const string losAngeles = "America/Los_Angeles";
        const string notes = "Dial in from the hotel";
        var instant = CallsEnvironment.At(1);
        // 1:00 AM in New York is the evening before in Los Angeles.
        var there = TimeZoneInfo.ConvertTimeFromUtc(instant, TimeZoneInfo.FindSystemTimeZoneById(losAngeles));

        await using var session = await env.OpenAsync(env.Dana.UserId, "/company/tasks", CallsEnvironment.NorthwindId, losAngeles);

        await session.RunAsync(nameof(Call_SeenFromAnotherTimeZone_ShowsTheSameInstant), async page =>
        {
            await ShowAllAsync(page);
            await Expect(Row(page, CallsEnvironment.CallFromNewYork))
                .ToContainTextAsync(Containing($"Due {Listed(there.Date)}, {TimeOfDay(there)} · {CallsEnvironment.NorthwindName} · Call"));

            var dialog = await EditAsync(page, CallsEnvironment.CallFromNewYork, "Edit call");
            await Expect(DueDate(dialog)).ToHaveValueAsync(Typed(there.Date));
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly(TimeOfDay(there)));

            await dialog.GetByLabel("Notes").FillAsync(notes);
            await SaveAsync(page, dialog);
        });

        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallFromNewYork && x.Description == notes);
        task.Should().NotBeNull();
        task.DueDate.Should().Be(instant);
    }

    [Fact(Timeout = 300_000)]
    public async Task EditCall_ChangingOnlyNotes_KeepsTheInstantAndTheCustomer()
    {
        const string notes = "Ask for the decision maker";

        await using var session = await OpenAsync("/company/tasks");

        await session.RunAsync(nameof(EditCall_ChangingOnlyNotes_KeepsTheInstantAndTheCustomer), async page =>
        {
            await ShowAllAsync(page);
            var dialog = await EditAsync(page, CallsEnvironment.CallOffTheQuarterHour, "Edit call");
            // Between two of the select's quarter hours: listed in its place, not snapped to either.
            await Expect(Field(dialog, "Time")).ToHaveValueAsync(Exactly("10:07 AM"));

            await dialog.GetByLabel("Notes").FillAsync(notes);
            await SaveAsync(page, dialog);

            await Expect(Row(page, CallsEnvironment.CallOffTheQuarterHour))
                .ToContainTextAsync(Containing($"Due {CallsEnvironment.ListedTomorrow}, 10:07 AM · {CallsEnvironment.ContosoName} · Call"));
        });

        // The update replaces the task, so the time, seconds included, and the customer have to go back as they were.
        var task = await env.WaitForTaskAsync(x => x.Name == CallsEnvironment.CallOffTheQuarterHour && x.Description == notes);
        task.Should().NotBeNull();
        task.DueDate.Should().Be(CallsEnvironment.OffTheQuarterHour);
        task.ObjectType.Should().Be(SalesRepModuleConstants.Tasks.CustomerObjectType);
        task.ObjectId.Should().Be(CallsEnvironment.ContosoId);
    }

    [Fact(Timeout = 300_000)]
    public async Task TimeField_OffersEveryQuarterHour_AndCannotBeEmptied()
    {
        var quarterHours = Enumerable.Range(0, 96).Select(i => TimeOfDay(DateTime.MinValue.AddMinutes(i * 15))).ToList();

        await using var session = await OpenAsync($"/company/my-customers/{CallsEnvironment.NorthwindId}");

        await session.RunAsync(nameof(TimeField_OffersEveryQuarterHour_AndCannotBeEmptied), async page =>
        {
            var dialog = await ScheduleCallAsync(page);
            var time = Field(dialog, "Time");

            await time.ClickAsync();
            await Expect(page.GetByRole(AriaRole.Option)).ToHaveTextAsync(quarterHours, new() { UseInnerText = true });
            await time.PressAsync("Escape");

            // Required: no Clear, and the keys that empty a field leave it as it was.
            await Expect(dialog.Locator(".time-select").GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true })).ToHaveCountAsync(0);
            await time.FocusAsync();
            await time.PressAsync("Backspace");
            await time.PressAsync("Delete");
            await Expect(time).ToHaveValueAsync(Exactly("9:00 AM"));
        });
    }

    private Task<StorefrontSession> OpenAsync(string path) => env.OpenAsync(env.Dana.UserId, path, CallsEnvironment.NorthwindId);

    /// <summary>A call about Contoso from the task form, at a day and time.</summary>
    private static async Task BookCallAsync(IPage page, string name, DateTime day, string time)
    {
        var dialog = await NewTaskAsync(page);
        await dialog.GetByLabel("Title").FillAsync(name);
        await PickAsync(page, Field(dialog, "Type"), Call);
        await PickCustomerAsync(page, dialog, CallsEnvironment.ContosoName);
        await TypeDayAsync(dialog, day);
        await PickAsync(page, Field(dialog, "Time"), time);
        await SaveAsync(page, dialog);
    }
}
