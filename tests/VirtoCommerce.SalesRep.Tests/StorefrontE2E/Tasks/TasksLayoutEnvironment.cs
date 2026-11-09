using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Tasks;

/// <summary>
/// Group 6: the tasks page's layout, in New York time. One rep whose customers and tasks carry the stress content of
/// QA's overflow audit: a 100-character customer name, two names that differ only at their ends, a name with no break
/// in it, an 80-character task title, an unbroken SKU and long notes. Today's five tasks fill the page as it opens,
/// one in every priority; tomorrow's four are the list under every filter. The tests only look, so they share the rep.
/// </summary>
public sealed class TasksLayoutEnvironment : StorefrontEnvironment
{
    public const string LongNameId = "e2e-org-layout-long";
    public const string LongName = "Northwind Wholesale Distribution and Logistics Services for the Greater Metropolitan Area Holding Co";
    public const string NorthId = "e2e-org-layout-north";
    public const string NorthName = "AGENT-TEST-Org-AcmeCorp-Regional-Distribution-Center-North";
    public const string SouthId = "e2e-org-layout-south";
    public const string SouthName = "AGENT-TEST-Org-AcmeCorp-Regional-Distribution-Center-South";
    public const string UnbrokenId = "e2e-org-layout-unbroken";
    public const string UnbrokenName = "CustomerWithAVeryLongUnbrokenNameABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public const string LongTitleTask = "Prepare the quarterly business review for the regional distribution partner meet";
    public const string SkuTask = "Reorder SKU100200300400500600700800900 for the spring catalog";
    public const string NotesTask = "Send the price list";
    public const string NoCustomerTask = "Plan the next route";
    public const string TomorrowsTask = "Confirm the delivery window";

    public const string LongNotes = "Bring the signed framework agreement, the updated price list for the spring catalog, the delivery schedule for " +
        "every regional warehouse and the samples requested at the last visit to the head office.";

    public static IReadOnlyList<string> CustomerNames { get; } = [LongName, NorthName, SouthName, UnbrokenName];

    public static IReadOnlyList<string> Priorities { get; } = ["Highest", "High", "Normal", "Low", "Lowest"];

    public string RepUserId { get; private set; }

    private protected override string BrowserTimeZone => NewYork.TimeZoneId;

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(LongNameId, org => org.Name = LongName);
        await context.SeedOrganizationAsync(NorthId, org => org.Name = NorthName);
        await context.SeedOrganizationAsync(SouthId, org => org.Name = SouthName);
        await context.SeedOrganizationAsync(UnbrokenId, org => org.Name = UnbrokenName);

        var rep = await context.CreateRepInStoreAsync("Layout", "Rep", "layout.rep@e2e.local", InProcessBackend.StoreId, LongNameId, NorthId, SouthId, UnbrokenId);
        RepUserId = rep.UserId;

        var today = NewYork.At(NewYork.Today, 12);
        await CreateTaskAsync(context, rep, LongTitleTask, today, customerId: LongNameId, priority: "Highest", description: LongNotes);
        await CreateTaskAsync(context, rep, SkuTask, today, customerId: NorthId, priority: "High");
        await CreateTaskAsync(context, rep, NotesTask, today, customerId: SouthId, priority: "Normal", description: LongNotes);
        await CreateTaskAsync(context, rep, "Call about the payment terms", today, customerId: UnbrokenId, priority: "Low");
        await CreateTaskAsync(context, rep, NoCustomerTask, today, priority: "Lowest");

        var tomorrow = NewYork.At(NewYork.Today.AddDays(1), 12);
        await CreateTaskAsync(context, rep, TomorrowsTask, tomorrow, customerId: LongNameId, priority: "Highest");
        await CreateTaskAsync(context, rep, "Review the open invoices", tomorrow, customerId: NorthId, priority: "High");
        await CreateTaskAsync(context, rep, "Book the showroom visit", tomorrow, customerId: SouthId, priority: "Normal");
        await CreateTaskAsync(context, rep, "Collect the signed order form", tomorrow, customerId: UnbrokenId, priority: "Low");
    }
}
