using System;
using System.Globalization;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using VirtoCommerce.TaskManagement.Core.Services;
using Xunit;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

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

    /// <summary>When "Review the contract" is due: tomorrow, off the quarter hours and to the second.</summary>
    public static DateTime TaskToRedateDue { get; } = NewYork.At(NewYork.Today.AddDays(1), 10, 20, 15);

    public string RepUserId { get; private set; }

    public string RepMemberId { get; private set; }

    private protected override string BrowserTimeZone => NewYork.TimeZoneId;

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = "Northwind Traders");

        var rep = await context.CreateRepInStoreAsync("Tom", "Rep", "tom.rep@e2e.local", InProcessBackend.StoreId, NorthwindId);
        RepUserId = rep.UserId;
        RepMemberId = rep.Id;

        var today = NewYork.Today;
        await CreateTaskAsync(context, TaskToRedate, TaskToRedateDue, $"type: \"Other\", priority: \"High\", description: \"{TaskToRedateNotes}\"");
        await CreateTaskAsync(context, OverdueTask, NewYork.At(today.AddDays(-1), 12));
        await CreateTaskAsync(context, TodaysTask, NewYork.At(today, 12));
        await CreateTaskAsync(context, LaterTask, NewYork.At(today.AddDays(2), 12));

        var completed = await CreateTaskAsync(context, CompletedTask, NewYork.At(today.AddDays(4), 12));
        SalesRepTestContext.Node(await ExecuteAsync(context, $"mutation {{ changeSalesRepTaskStatus(command: {{ id: \"{completed}\", completed: true }}) {{ id }} }}"), "changeSalesRepTaskStatus");

        // Closed without being completed: canceled, which only task management itself can do.
        var tasks = context.GetRequiredService<IWorkTaskService>();
        await tasks.FinishAsync(await CreateTaskAsync(context, CanceledTask, NewYork.At(today.AddDays(5), 12)), completed: false, result: null);

        // The storefront's inputs require a due date; the admin app's do not.
        var dateless = await tasks.GetByIdAsync(await CreateTaskAsync(context, DatelessTask, NewYork.At(today.AddDays(1), 12)));
        dateless.DueDate = null;
        await tasks.SaveChangesAsync([dateless]);
    }

    private async Task<string> CreateTaskAsync(SalesRepTestContext context, string name, DateTime dueDate, string fields = null)
    {
        var due = dueDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var more = fields == null ? string.Empty : $", {fields}";
        var json = await ExecuteAsync(context, $"mutation {{ createSalesRepTask(command: {{ name: \"{name}\", dueDate: \"{due}\"{more} }}) {{ id }} }}");

        return SalesRepTestContext.Node(json, "createSalesRepTask").GetProperty("id").GetString();
    }

    private Task<string> ExecuteAsync(SalesRepTestContext context, string mutation)
        => context.ExecuteGraphQlAsync(mutation, userId: RepUserId, memberId: RepMemberId, permissions: [SalesRepModuleConstants.Security.Permissions.Access]);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TasksCollection : ICollectionFixture<TasksEnvironment>
{
    public const string Name = "Storefront E2E: tasks";
}
