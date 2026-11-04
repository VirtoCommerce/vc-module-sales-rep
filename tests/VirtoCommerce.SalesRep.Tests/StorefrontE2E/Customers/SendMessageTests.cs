using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;
using static VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure.HubControls;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>
/// "Send a message" from its three doors: My customers' own button opens it with nobody picked, a customer's row with
/// that customer picked and room for more, and a customer's quick actions locked to that customer. A test that sends
/// signs in as a rep of its own and writes to customers of its own, so whom the message reaches is the test's to say;
/// the push channel is the harness's capturing double, read back by the message's text.
/// </summary>
[Collection(CustomersCollection.Name)]
[Trait("Category", "StorefrontE2E")]
[Trait("Category", "IntegrationTest")]
public sealed class SendMessageTests(CustomersEnvironment env)
{
    private const string Popup = "Send a message";
    private const string NeedsACustomer = "Select at least one customer.";
    private const string Sent = "Your message has been sent.";

    [Fact(Timeout = 300_000)]
    public async Task SendMessage_FromThePageButton_ReachesEachContactOnce()
    {
        const string message = "Our spring catalog is out.";
        var northwind = await env.NewCustomerAsync("Northwind Traders");
        var tailspin = await env.NewCustomerAsync("Tailspin Toys");
        var northwindContact = await env.NewContactAsync(northwind);
        var tailspinContact = await env.NewContactAsync(tailspin);
        var sharedContact = await env.NewContactAsync(northwind, tailspin);
        var rep = await env.NewRepAsync(northwind, tailspin);

        await using var session = await env.OpenAsync(rep.UserId, "/company/my-customers");

        await session.RunAsync(nameof(SendMessage_FromThePageButton_ReachesEachContactOnce), async page =>
        {
            await SendMessageButton(page).ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, Popup);

            // Nobody picked, and no complaint about it before the rep has picked anyone.
            await Expect(dialog.GetByText("Reaches every member of the selected customers", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(RemoveButtons(dialog)).ToHaveCountAsync(0);
            await Expect(dialog.GetByText(NeedsACustomer, new() { Exact = true })).ToHaveCountAsync(0);
            await Expect(SendButton(dialog)).ToBeDisabledAsync();

            // The rep's two customers and nobody else's: not the group's Fabrikam and Contoso, not Adventure Works.
            await Customers(dialog).ClickAsync();
            await Expect(page.GetByRole(AriaRole.Option)).ToHaveCountAsync(2);
            await CustomerOption(page, "Northwind Traders").ClickAsync();
            await CustomerOption(page, "Tailspin Toys").ClickAsync();
            await CloseListAsync(page, dialog);
            await Expect(RemoveButton(dialog, "Northwind Traders")).ToBeVisibleAsync();
            await Expect(RemoveButton(dialog, "Tailspin Toys")).ToBeVisibleAsync();

            // Send waits for a message and a channel as well.
            await Expect(SendButton(dialog)).ToBeDisabledAsync();
            await MessageBox(dialog).FillAsync(message);
            await Expect(SendButton(dialog)).ToBeDisabledAsync();
            await TickAsync(dialog, "Push notification");
            await Expect(SendButton(dialog)).ToBeEnabledAsync();

            await SendButton(dialog).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(page.GetByText(Sent, new() { Exact = true })).ToBeVisibleAsync();
        });

        session.GraphQlErrors.Should().BeEmpty();

        // One push for both customers. The contact of both is in it once; the rep, a contact of both too, is left out.
        (await env.PushesAsync(message)).Should().ContainSingle()
            .Which.MemberIds.Should().BeEquivalentTo([northwindContact, tailspinContact, sharedContact]);
    }

    [Fact(Timeout = 300_000)]
    public async Task SendMessage_FromARow_EmptyListDisablesSend()
    {
        await using var session = await env.OpenAsync(env.RepUserId, "/company/my-customers");

        await session.RunAsync(nameof(SendMessage_FromARow_EmptyListDisablesSend), async page =>
        {
            await RowSendMessageButton(page, CustomersEnvironment.FabrikamName).ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, Popup);

            // The row's customer comes picked, and the rep can add another.
            await Expect(RemoveButton(dialog, CustomersEnvironment.FabrikamName)).ToBeVisibleAsync();
            await Customers(dialog).ClickAsync();
            await CustomerOption(page, CustomersEnvironment.ContosoName).ClickAsync();
            await CloseListAsync(page, dialog);
            await MessageBox(dialog).FillAsync("Never sent: the rep removes everybody first.");
            await TickAsync(dialog, "Push notification");
            await Expect(SendButton(dialog)).ToBeEnabledAsync();

            // Removing a customer hands the focus to the remove button left; removing the last takes the list away, and
            // the focus goes to the field, which now says why Send is off.
            await RemoveButton(dialog, CustomersEnvironment.FabrikamName).ClickAsync();
            await Expect(RemoveButton(dialog, CustomersEnvironment.ContosoName)).ToBeFocusedAsync();
            await RemoveButton(dialog, CustomersEnvironment.ContosoName).ClickAsync();
            await Expect(FocusedCustomersField(page)).ToHaveCountAsync(1);
            await Expect(dialog.GetByText(NeedsACustomer, new() { Exact = true })).ToBeVisibleAsync();
            await Expect(SendButton(dialog)).ToBeDisabledAsync();

            await CancelButton(dialog).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
        });
    }

    [Fact(Timeout = 300_000)]
    public async Task SendMessage_FromQuickActions_StaysOnThatCustomer()
    {
        const string message = "Your order has shipped.";
        var wingtip = await env.NewCustomerAsync("Wingtip Toys");
        var litware = await env.NewCustomerAsync("Litware Inc");
        var wingtipContact = await env.NewContactAsync(wingtip);
        await env.NewContactAsync(litware);
        var rep = await env.NewRepAsync(wingtip, litware);

        await using var session = await env.OpenAsync(rep.UserId, $"/company/my-customers/{wingtip}");

        await session.RunAsync(nameof(SendMessage_FromQuickActions_StaysOnThatCustomer), async page =>
        {
            await QuickAction(page, "Send email").ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, Popup);

            // That customer, shown and never changed: no picker, nothing to remove or clear.
            await Expect(dialog.GetByText("Reaches every member of Wingtip Toys", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(dialog.GetByText("Wingtip Toys", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(Customers(dialog)).ToHaveCountAsync(0);
            await Expect(RemoveButtons(dialog)).ToHaveCountAsync(0);
            await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Clear all", Exact = true })).ToHaveCountAsync(0);

            await MessageBox(dialog).FillAsync(message);
            await TickAsync(dialog, "Push notification");
            await SendButton(dialog).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(page.GetByText(Sent, new() { Exact = true })).ToBeVisibleAsync();
        });

        session.GraphQlErrors.Should().BeEmpty();

        // Wingtip's contacts only: Litware, the rep's other customer, gets nothing.
        (await env.PushesAsync(message)).Should().ContainSingle()
            .Which.MemberIds.Should().BeEquivalentTo([wingtipContact]);
    }

    [Fact(Timeout = 300_000)]
    public async Task SendMessage_CustomerNoLongerServed_KeepsTheDraft()
    {
        const string message = "Meet us at the trade show.";
        var proseware = await env.NewCustomerAsync("Proseware Inc");
        var coho = await env.NewCustomerAsync("Coho Winery");
        var prosewareContact = await env.NewContactAsync(proseware);
        var cohoContact = await env.NewContactAsync(coho);
        var rep = await env.NewRepAsync(proseware, coho);

        await using var session = await env.OpenAsync(rep.UserId, "/company/my-customers");

        await session.RunAsync(nameof(SendMessage_CustomerNoLongerServed_KeepsTheDraft), async page =>
        {
            await SendMessageButton(page).ClickAsync(new() { Timeout = 60_000 });
            var dialog = Dialog(page, Popup);
            await Customers(dialog).ClickAsync();
            await CustomerOption(page, "Proseware Inc").ClickAsync();
            await CustomerOption(page, "Coho Winery").ClickAsync();
            await CloseListAsync(page, dialog);
            await MessageBox(dialog).FillAsync(message);
            await TickAsync(dialog, "Push notification");

            // By the time the rep sends, Coho is no longer theirs: the whole send is refused, and the draft stays to retry.
            var removed = await env.StopServingAsync(rep.UserId, coho);
            await SendButton(dialog).ClickAsync();

            await Expect(page.GetByText("The message could not be sent. Please try again.", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(RemoveButton(dialog, "Proseware Inc")).ToBeVisibleAsync();
            await Expect(RemoveButton(dialog, "Coho Winery")).ToBeVisibleAsync();
            await Expect(MessageBox(dialog)).ToHaveValueAsync(message);
            await Expect(dialog.GetByRole(AriaRole.Checkbox, new() { Name = "Push notification", Exact = true })).ToBeCheckedAsync();
            (await env.PushesAsync(message)).Should().BeEmpty();

            // Coho is the rep's again: the same draft goes.
            await env.ServeAgainAsync(removed);
            await SendButton(dialog).ClickAsync();
            await Expect(dialog).ToHaveCountAsync(0);
            await Expect(page.GetByText(Sent, new() { Exact = true })).ToBeVisibleAsync();
        });

        (await env.PushesAsync(message)).Should().ContainSingle()
            .Which.MemberIds.Should().BeEquivalentTo([prosewareContact, cohoContact]);
    }

    /// <summary>My customers' own Send message button, by its test id: every row has a "Send message" button too.</summary>
    private static ILocator SendMessageButton(IPage page) => page.Locator("[data-test-id='my-customers-send-message-button']");

    /// <summary>A customer row's Send message button, the envelope beside the call button.</summary>
    private static ILocator RowSendMessageButton(IPage page, string customer)
        => page.GetByRole(AriaRole.Row)
            .Filter(new() { Has = page.GetByRole(AriaRole.Link, new() { Name = customer, Exact = true }) })
            .GetByRole(AriaRole.Button, new() { Name = "Send message", Exact = true });

    /// <summary>The Customers field: a VcSelect that picks several and searches as the rep types.</summary>
    private static ILocator Customers(ILocator dialog) => Field(dialog, "Customers");

    /// <summary>A customer in the Customers list, named by its name, then its location when it has one.</summary>
    private static ILocator CustomerOption(IPage page, string name)
        => page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex($"^{Regex.Escape(name)}(\\s|$)") });

    /// <summary>
    /// Closes the Customers list, which stays open while the rep picks, with a click outside it: on the popup's title,
    /// which the list never covers. A click on a field or a row under the list would land on an option instead.
    /// </summary>
    private static async Task CloseListAsync(IPage page, ILocator dialog)
    {
        await dialog.GetByRole(AriaRole.Heading, new() { Name = Popup, Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Listbox)).ToHaveCountAsync(0);
    }

    /// <summary>A picked customer's remove button, in the recipients list under the field.</summary>
    private static ILocator RemoveButton(ILocator dialog, string customer)
        => dialog.GetByRole(AriaRole.Button, new() { Name = $"Remove {customer}", Exact = true });

    private static ILocator RemoveButtons(ILocator dialog) => dialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Remove ") });

    /// <summary>The message box; its name carries the required mark ("Message*").</summary>
    private static ILocator MessageBox(ILocator dialog) => dialog.GetByRole(AriaRole.Textbox, new() { Name = "Message" });

    private static ILocator SendButton(ILocator dialog) => dialog.GetByRole(AriaRole.Button, new() { Name = "Send", Exact = true });

    /// <summary>
    /// The focused element, when it is the Customers field: VcSelect's container, which takes the focus without opening
    /// the list, as the combobox inside it would. Around the combobox but not the Title field: the dialog and the form
    /// hold the combobox too.
    /// </summary>
    private static ILocator FocusedCustomersField(IPage page)
        => page.Locator(":focus")
            .Filter(new() { Has = page.GetByRole(AriaRole.Combobox, new() { Name = "Customers", Exact = true }) })
            .Filter(new() { HasNot = page.GetByRole(AriaRole.Textbox, new() { Name = "Title", Exact = true }) });
}
