using System;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calendar;

/// <summary>Group 2: a rep with one task due today, seeded through the real mutation the storefront uses.</summary>
public sealed class CalendarEnvironment : StorefrontEnvironment
{
    public const string NorthwindId = "e2e-org-northwind";
    public const string SeededTaskName = "Call Northwind about the renewal";

    public string RepUserId { get; private set; }

    public string RepMemberId { get; private set; }

    public string SeededTaskId { get; private set; }

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = "Northwind Traders");

        var rep = await context.CreateRepInStoreAsync("Carl", "Rep", "carl.rep@e2e.local", InProcessBackend.StoreId, NorthwindId);
        RepUserId = rep.UserId;
        RepMemberId = rep.Id;

        // Noon today in the machine's local time, as an instant: the calendar opens on the browser's local day, and
        // the browser runs on this machine, so the task lands on the selected day whatever the UTC offset.
        var dueDate = DateTime.Today.AddHours(12).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var json = await context.ExecuteGraphQlAsync(
            $"mutation {{ createSalesRepTask(command: {{ name: \"{SeededTaskName}\", dueDate: \"{dueDate}\" }}) {{ id }} }}",
            userId: rep.UserId,
            memberId: rep.Id,
            permissions: [SalesRepModuleConstants.Security.Permissions.Access]);

        SeededTaskId = SalesRepTestContext.Node(json, "createSalesRepTask").GetProperty("id").GetString();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CalendarCollection : ICollectionFixture<CalendarEnvironment>
{
    public const string Name = "Storefront E2E: calendar";
}
