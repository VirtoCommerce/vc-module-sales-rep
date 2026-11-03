using System;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>
/// Group 4: a rep's plain tasks on the tasks page, in New York time: open ones due yesterday, today and later, one each
/// completed, canceled and without a due date, and one with a type, a priority, notes and a time to the second. Each is
/// created through the real mutation; the states the storefront cannot produce (canceled, no due date) are then set
/// through task management's own service, as its admin app would set them.
/// </summary>
public sealed class TasksEnvironment : StorefrontEnvironment
{
    public const string NorthwindId = "e2e-org-northwind";

    public const string TaskToRedate = "Review the contract";
    public const string TaskToRedateNotes = "Bring the redlines";
    public const string CanceledTask = "Canceled visit";
    public const string CompletedTask = "Finished survey";
    public const string DatelessTask = "Undated follow-up";
    public const string OverdueTask = "Chase the late payment";
    public const string TodaysTask = "Visit the showroom";
    public const string LaterTask = "Plan the trade show";

    /// <summary>When "Review the contract" is due on its day: off the quarter hours and to the second.</summary>
    public static TimeSpan TaskToRedateTime { get; } = new(10, 20, 15);

    public string RepUserId { get; private set; }

    private protected override string BrowserTimeZone => NewYork.TimeZoneId;

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = "Northwind Traders");

        var rep = await context.CreateRepInStoreAsync("Tom", "Rep", "tom.rep@e2e.local", InProcessBackend.StoreId, NorthwindId);
        RepUserId = rep.UserId;

        var today = NewYork.Today;
        await CreateTaskAsync(context, rep, TaskToRedate, NewYork.At(today.AddDays(1) + TaskToRedateTime), type: "Other", priority: "High", description: TaskToRedateNotes);
        await CreateTaskAsync(context, rep, OverdueTask, NewYork.At(today.AddDays(-1), 12));
        await CreateTaskAsync(context, rep, TodaysTask, NewYork.At(today, 12));
        await CreateTaskAsync(context, rep, LaterTask, NewYork.At(today.AddDays(2), 12));

        var completed = await CreateTaskAsync(context, rep, CompletedTask, NewYork.At(today.AddDays(4), 12));
        SalesRepTestContext.Node(await ExecuteAsRepAsync(context, rep, $"mutation {{ changeSalesRepTaskStatus(command: {{ id: \"{completed}\", completed: true }}) {{ id }} }}"), "changeSalesRepTaskStatus");

        // Closed without being completed: canceled, which only task management itself can do.
        var tasks = context.GetRequiredService<IWorkTaskService>();
        await tasks.FinishAsync(await CreateTaskAsync(context, rep, CanceledTask, NewYork.At(today.AddDays(5), 12)), completed: false, result: null);

        // The storefront's inputs require a due date; the admin app's do not.
        var dateless = await tasks.GetByIdAsync(await CreateTaskAsync(context, rep, DatelessTask, NewYork.At(today.AddDays(1), 12)));
        dateless.DueDate = null;
        await tasks.SaveChangesAsync([dateless]);
    }
}
