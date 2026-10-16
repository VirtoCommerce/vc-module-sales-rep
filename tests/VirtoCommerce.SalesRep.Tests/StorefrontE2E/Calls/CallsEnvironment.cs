using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>
/// Group 3: a rep with calls and plain tasks due tomorrow, seeded through the real mutation the storefront uses, one
/// task per test that writes to it (xunit promises no order). The rep serves Northwind and Contoso, served Fabrikam
/// until after a call about it was booked, and never served Adventure Works. The browser runs in New York time, so
/// every time of day here is New York time, whatever the machine's zone.
/// </summary>
public sealed class CallsEnvironment : StorefrontEnvironment
{
    public const string TimeZoneId = "America/New_York";

    public const string NorthwindId = "e2e-org-northwind";
    public const string NorthwindName = "Northwind Traders";
    public const string ContosoId = "e2e-org-contoso";
    public const string ContosoName = "Contoso Ltd";
    public const string FabrikamId = "e2e-org-fabrikam";
    public const string FabrikamName = "Fabrikam Inc";
    public const string AdventureWorksId = "e2e-org-adventure-works";
    public const string AdventureWorksName = "Adventure Works";

    public const string ListedCall = "Call Contoso about the renewal";
    public const string ListedCallWithoutCustomer = "Call back the front desk";
    public const string ListedTask = "Send the price list";
    public const string CallToEdit = "Demo follow-up";
    public const string CallToUnlink = "Pricing question";
    public const string TaskToTurnIntoCall = "Prepare the quote";
    public const string CallAboutFormerCustomer = "Call about the overdue invoice";

    private static readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>Tomorrow in New York: every seeded task is due then, and the tests book their calls for it.</summary>
    public static DateTime Tomorrow { get; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone).Date.AddDays(1);

    /// <summary>Tomorrow as the date picker takes it typed (en-US).</summary>
    public static string TypedTomorrow => Tomorrow.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Tomorrow as the task list prints it (the storefront's "short" date, en-US).</summary>
    public static string ListedTomorrow => Tomorrow.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    public string RepUserId { get; private set; }

    public string RepMemberId { get; private set; }

    private protected override string BrowserTimeZone => TimeZoneId;

    /// <summary>The instant of a New York time of day tomorrow.</summary>
    public static DateTime At(int hour, int minute = 0)
        => TimeZoneInfo.ConvertTimeToUtc(Tomorrow.AddHours(hour).AddMinutes(minute), _timeZone);

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = NorthwindName);
        await context.SeedOrganizationAsync(ContosoId, org => org.Name = ContosoName);
        await context.SeedOrganizationAsync(FabrikamId, org => org.Name = FabrikamName);
        await context.SeedOrganizationAsync(AdventureWorksId, org => org.Name = AdventureWorksName);

        var rep = await context.CreateRepInStoreAsync("Carl", "Rep", "carl.rep@e2e.local", InProcessBackend.StoreId, NorthwindId, ContosoId, FabrikamId);
        RepUserId = rep.UserId;
        RepMemberId = rep.Id;

        // The customer picker lists the rep's customers through the index.
        await context.IndexMembersAsync(NorthwindId, ContosoId, FabrikamId, AdventureWorksId);

        await CreateTaskAsync(context, ListedCall, At(10, 15), SalesRepModuleConstants.Tasks.CallType, ContosoId);
        await CreateTaskAsync(context, ListedCallWithoutCustomer, At(11), SalesRepModuleConstants.Tasks.CallType);
        await CreateTaskAsync(context, ListedTask, At(12), "Other");
        await CreateTaskAsync(context, CallToEdit, At(10), SalesRepModuleConstants.Tasks.CallType, ContosoId);
        await CreateTaskAsync(context, CallToUnlink, At(13), SalesRepModuleConstants.Tasks.CallType, NorthwindId);
        await CreateTaskAsync(context, TaskToTurnIntoCall, At(12), "Other");
        await CreateTaskAsync(context, CallAboutFormerCustomer, At(15), SalesRepModuleConstants.Tasks.CallType, FabrikamId);

        // Then the rep stops serving Fabrikam: the call stays theirs, the customer is no longer theirs to see.
        var membership = (await context.GetMembershipsAsync(RepUserId)).Single(x => x.OrganizationId == FabrikamId);
        await context.GetRequiredService<IOrganizationMembershipService>().DeleteAsync([membership.Id]);
    }

    private async Task CreateTaskAsync(SalesRepTestContext context, string name, DateTime dueDate, string type, string customerId = null)
    {
        var due = dueDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var customer = customerId == null ? string.Empty : $", customerId: \"{customerId}\"";
        var json = await context.ExecuteGraphQlAsync(
            $"mutation {{ createSalesRepTask(command: {{ name: \"{name}\", dueDate: \"{due}\", type: \"{type}\"{customer} }}) {{ id }} }}",
            userId: RepUserId,
            memberId: RepMemberId,
            permissions: [SalesRepModuleConstants.Security.Permissions.Access]);

        SalesRepTestContext.Node(json, "createSalesRepTask");
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CallsCollection : ICollectionFixture<CallsEnvironment>
{
    public const string Name = "Storefront E2E: calls";
}
