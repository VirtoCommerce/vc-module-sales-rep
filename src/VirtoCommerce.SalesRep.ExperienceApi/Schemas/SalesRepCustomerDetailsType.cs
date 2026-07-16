using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerDetailsType : ExtendableGraphType<SalesRepCustomerDetails>
{
    public SalesRepCustomerDetailsType()
    {
        Name = "SalesRepCustomerDetails";

        Field(x => x.OrganizationId, nullable: false).Description("Organization (customer) id.");
        Field(x => x.OrganizationName, nullable: true).Description("Organization (customer) name.");
        Field(x => x.IconUrl, nullable: true).Description("URL of the organization's icon.");
        Field(x => x.Phone, nullable: true).Description("Primary phone number (the primary contact's, falling back to the organization's).");
        Field(x => x.AccountType, nullable: true).Description("Account type — the organization's business category.");
        Field<SalesRepAddressType>("address")
            .Description("The organization's default address (structured; the storefront formats it, e.g. \"City, Region\").")
            .Resolve(context => context.Source.Address);

        Field<SalesRepContactType>("primaryContact")
            .Description("Primary contact of the organization (its owner, or the first contact member).")
            .Resolve(context => context.Source.PrimaryContact);
    }
}
