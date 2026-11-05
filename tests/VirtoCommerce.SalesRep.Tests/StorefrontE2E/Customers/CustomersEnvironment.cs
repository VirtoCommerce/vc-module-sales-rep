using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>
/// Group 1: a rep serving two organizations, one of which they already sold to, and an organization they do not serve.
/// A test that sends a message sends it to customers of its own (<see cref="NewCustomerAsync"/>, with contacts from
/// <see cref="NewContactAsync"/>) as a rep of its own: every rep is a contact of the customers they serve, so on shared
/// customers whom a message reaches would depend on which tests had made their reps first.
/// </summary>
public sealed class CustomersEnvironment : StorefrontEnvironment
{
    public const string FabrikamId = "e2e-org-fabrikam";
    public const string FabrikamName = "Fabrikam Inc";
    public const string FabrikamOwnerId = "e2e-contact-pat";
    public const string FabrikamOwnerName = "Pat Buyer";
    public const string FabrikamOwnerPhone = "+1 555 0100";
    public const string ContosoId = "e2e-org-contoso";
    public const string ContosoName = "Contoso Ltd";
    public const string AdventureWorksId = "e2e-org-adventure-works";
    public const string OrderId = "e2e-order-1001";
    public const string OrderNumber = "ORD-1001";

    private int _customers;
    private int _contacts;

    public string RepUserId { get; private set; }

    public string RepMemberId { get; private set; }

    /// <summary>A customer of the test's own, indexed, as the hub's lists and its customer picker read customers from the index.</summary>
    internal Task<string> NewCustomerAsync(string name)
    {
        var id = $"e2e-org-{(++_customers).ToString(CultureInfo.InvariantCulture)}";
        return RunExclusiveAsync(async context =>
        {
            await context.SeedOrganizationAsync(id, org => org.Name = name);
            await context.IndexMembersAsync(id);
            return id;
        });
    }

    /// <summary>A contact of the given customers: a message to any of them reaches it.</summary>
    internal Task<string> NewContactAsync(params string[] customerIds)
    {
        var n = (++_contacts).ToString(CultureInfo.InvariantCulture);
        var id = $"e2e-contact-{n}";
        return RunExclusiveAsync(async context =>
        {
            await context.SeedContactAsync(id, contact =>
            {
                contact.FirstName = "Contact";
                contact.LastName = n;
                contact.FullName = $"Contact {n}";
                contact.Organizations = [.. customerIds];
            });
            return id;
        });
    }

    /// <summary>
    /// The pushes sent with the message's text, read between requests. The push channel is the harness's capturing
    /// double, one list for the whole group, so a test finds its own by a text no other test sends.
    /// </summary>
    internal Task<IList<PushMessage>> PushesAsync(string message)
        => RunExclusiveAsync(context => Task.FromResult<IList<PushMessage>>(context
            .GetRequiredService<TestGraphQlConfiguration.CapturingPushMessageService>().Saved
            .Where(x => x.ShortMessage == message)
            .ToList()));

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        // The profile's "primary contact" is the organization's owner (else its oldest contact), so Fabrikam gets a
        // buyer as owner; the rep is a contact of the organization too and would otherwise be picked.
        await context.SeedOrganizationAsync(FabrikamId, org =>
        {
            org.Name = FabrikamName;
            org.OwnerId = FabrikamOwnerId;
        });
        await context.SeedContactAsync(FabrikamOwnerId, contact =>
        {
            contact.FirstName = "Pat";
            contact.LastName = "Buyer";
            contact.FullName = FabrikamOwnerName;
            contact.Phones = [FabrikamOwnerPhone];
            contact.Organizations = [FabrikamId];
        });
        await context.SeedOrganizationAsync(ContosoId, org => org.Name = ContosoName);
        await context.SeedOrganizationAsync(AdventureWorksId, org => org.Name = "Adventure Works");

        var rep = await context.CreateRepInStoreAsync("Rita", "Rep", "rita.rep@e2e.local", InProcessBackend.StoreId, FabrikamId, ContosoId);
        RepUserId = rep.UserId;
        RepMemberId = rep.Id;

        // An order the rep placed for Fabrikam three days ago: the "My last order" cell and the profile's order list.
        OrderSeeder.Seed(context, OrderId, FabrikamId, OrderNumber, DateTime.UtcNow.AddDays(-3),
            organizationName: FabrikamName, createdByUserId: rep.UserId, itemsCount: 2);
        await context.IndexOrdersAsync(OrderId);
        await context.IndexMembersAsync(FabrikamId, ContosoId, AdventureWorksId);
    }
}
