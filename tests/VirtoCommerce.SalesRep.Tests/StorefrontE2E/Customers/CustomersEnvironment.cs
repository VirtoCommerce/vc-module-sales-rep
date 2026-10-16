using System;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Customers;

/// <summary>Group 1: a rep serving two organizations, one of which they already sold to.</summary>
public sealed class CustomersEnvironment : StorefrontEnvironment
{
    public const string FabrikamId = "e2e-org-fabrikam";
    public const string FabrikamName = "Fabrikam Inc";
    public const string FabrikamOwnerId = "e2e-contact-pat";
    public const string FabrikamOwnerName = "Pat Buyer";
    public const string FabrikamOwnerPhone = "+1 555 0100";
    public const string ContosoId = "e2e-org-contoso";
    public const string ContosoName = "Contoso Ltd";
    public const string OrderId = "e2e-order-1001";
    public const string OrderNumber = "ORD-1001";

    public string RepUserId { get; private set; }

    public string RepMemberId { get; private set; }

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

        var rep = await context.CreateRepInStoreAsync("Rita", "Rep", "rita.rep@e2e.local", InProcessBackend.StoreId, FabrikamId, ContosoId);
        RepUserId = rep.UserId;
        RepMemberId = rep.Id;

        // An order the rep placed for Fabrikam three days ago: the "My last order" cell and the profile's order list.
        OrderSeeder.Seed(context, OrderId, FabrikamId, OrderNumber, DateTime.UtcNow.AddDays(-3),
            organizationName: FabrikamName, createdByUserId: rep.UserId, itemsCount: 2);
        await context.IndexOrdersAsync(OrderId);
        await context.IndexMembersAsync(FabrikamId, ContosoId);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CustomersCollection : ICollectionFixture<CustomersEnvironment>
{
    public const string Name = "Storefront E2E: my customers";
}
