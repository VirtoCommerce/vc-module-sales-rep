using System;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Calls;

/// <summary>
/// Group 3: Carl's calls and plain tasks, seeded through the real mutation the storefront uses, one task per test that
/// writes to it (xunit promises no order). Carl serves Northwind and Contoso, served Fabrikam until after a call about
/// it was booked, and never served Adventure Works. A test that books a call, or takes a customer away, signs in as a
/// rep of its own (<see cref="NewRepAsync()"/>): its rows never push Carl's off the task list's first page, and what it
/// changes is nobody else's. The browser runs in New York time, so every time of day here is New York time, whatever
/// the machine's zone.
/// </summary>
public sealed class CallsEnvironment : StorefrontEnvironment
{
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

    public const string CallOnTheRepeatedHour = "Call on the repeated hour";
    public const string CallFromNewYork = "Call from New York";
    public const string CallOffTheQuarterHour = "Call off the quarter hour";

    private const string Call = SalesRepModuleConstants.Tasks.CallType;

    /// <summary>Tomorrow in New York: Carl's seeded tasks are due then, and most tests book their calls for it.</summary>
    public static DateTime Tomorrow { get; } = NewYork.Today.AddDays(1);

    /// <summary>Tomorrow as the date picker takes it typed (en-US).</summary>
    public static string TypedTomorrow => HubControls.Typed(Tomorrow);

    /// <summary>Tomorrow as the task list prints it (the storefront's "short" date, en-US).</summary>
    public static string ListedTomorrow => HubControls.Listed(Tomorrow);

    /// <summary>The next night the clocks go back: 1:30 AM happens twice.</summary>
    public static DateTime RepeatedHourDay { get; } = NewYork.NextDayRepeating(1, 30);

    /// <summary>The instant of "Call on the repeated hour": the second 1:30 AM that night, in standard time.</summary>
    public static DateTime SecondOneThirty { get; } = NewYork.AtSecondPass(RepeatedHourDay, 1, 30);

    /// <summary>The instant of "Call off the quarter hour": between two of the time select's steps, with seconds.</summary>
    public static DateTime OffTheQuarterHour { get; } = NewYork.At(Tomorrow, 10, 7, 30);

    public string RepUserId { get; private set; }

    private protected override string BrowserTimeZone => NewYork.TimeZoneId;

    /// <summary>The instant of a New York time of day tomorrow.</summary>
    public static DateTime At(int hour, int minute = 0) => NewYork.At(Tomorrow, hour, minute);

    /// <summary>A rep of the test's own, serving Northwind and Contoso.</summary>
    internal Task<SalesRepDetails> NewRepAsync() => NewRepAsync(NorthwindId, ContosoId);

    /// <summary>
    /// Takes a customer away from a rep while a page may be open, between requests, and returns the membership that
    /// granted it, for <see cref="ServeAgainAsync"/>.
    /// </summary>
    internal Task<OrganizationMembership> StopServingAsync(string userId, string organizationId)
        => RunExclusiveAsync(async context =>
        {
            var membership = (await context.GetMembershipsAsync(userId)).Single(x => x.OrganizationId == organizationId);
            await context.GetRequiredService<IOrganizationMembershipService>().DeleteAsync([membership.Id]);
            return membership;
        });

    /// <summary>Gives the customer back, with the role the removed membership carried.</summary>
    internal Task ServeAgainAsync(OrganizationMembership membership)
        => RunExclusiveAsync(context =>
        {
            var role = membership.Roles.Single();
            return context.AddMembershipAsync(membership.UserId, membership.OrganizationId, new Role { Id = role.RoleId, Name = role.RoleName });
        });

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = NorthwindName);
        await context.SeedOrganizationAsync(ContosoId, org => org.Name = ContosoName);
        await context.SeedOrganizationAsync(FabrikamId, org => org.Name = FabrikamName);
        await context.SeedOrganizationAsync(AdventureWorksId, org => org.Name = AdventureWorksName);

        var carl = await context.CreateRepInStoreAsync("Carl", "Rep", "carl.rep@e2e.local", InProcessBackend.StoreId, NorthwindId, ContosoId, FabrikamId);
        RepUserId = carl.UserId;

        // The customer picker lists the rep's customers through the index.
        await context.IndexMembersAsync(NorthwindId, ContosoId, FabrikamId, AdventureWorksId);

        await CreateTaskAsync(context, carl, ListedCall, At(10, 15), Call, ContosoId);
        await CreateTaskAsync(context, carl, ListedCallWithoutCustomer, At(11), Call);
        await CreateTaskAsync(context, carl, ListedTask, At(12), "Other");
        await CreateTaskAsync(context, carl, CallToEdit, At(10), Call, ContosoId);
        await CreateTaskAsync(context, carl, CallToUnlink, At(13), Call, NorthwindId);
        await CreateTaskAsync(context, carl, TaskToTurnIntoCall, At(12), "Other");
        await CreateTaskAsync(context, carl, CallAboutFormerCustomer, At(15), Call, FabrikamId);
        await CreateTaskAsync(context, carl, CallOnTheRepeatedHour, SecondOneThirty, Call, ContosoId);
        await CreateTaskAsync(context, carl, CallFromNewYork, At(1), Call, NorthwindId);
        await CreateTaskAsync(context, carl, CallOffTheQuarterHour, OffTheQuarterHour, Call, ContosoId);

        // Then Carl stops serving Fabrikam: the call stays his, the customer is no longer his to see.
        await StopServingAsync(RepUserId, FabrikamId);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CallsCollection : ICollectionFixture<CallsEnvironment>
{
    public const string Name = "Storefront E2E: calls";
}
