using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>
/// A customer address projected for Sales Rep storefront views (VCST-5304/5308) — the organization's default
/// address shown under its name. A thin, self-contained graph type over the platform <see cref="Address"/> that
/// mirrors each X-module's own address type (X-Order's <c>OrderAddressType</c>, X-Cart's <c>CartAddressType</c>),
/// so this module stays independent of the profile/cart/order X-APIs. The storefront formats the display string
/// (e.g. "City, Region"); this only exposes the structured parts.
/// </summary>
public class SalesRepAddressType : ExtendableGraphType<Address>
{
    public SalesRepAddressType()
    {
        Name = "SalesRepAddress";

        Field<StringGraphType>("id").Description("Id.").Resolve(context => context.Source.Key);
        Field(x => x.Key, nullable: true).Description("Id.");
        Field(x => x.IsDefault, nullable: false).Description("Whether this is the organization's default address.");
        Field(x => x.City, nullable: true).Description("City.");
        Field(x => x.CountryCode, nullable: true).Description("Country code.");
        Field(x => x.CountryName, nullable: true).Description("Country name.");
        Field(x => x.Email, nullable: true).Description("Email.");
        Field(x => x.FirstName, nullable: true).Description("First name.");
        Field(x => x.MiddleName, nullable: true).Description("Middle name.");
        Field(x => x.LastName, nullable: true).Description("Last name.");
        Field(x => x.Line1, nullable: true).Description("Line1.");
        Field(x => x.Line2, nullable: true).Description("Line2.");
        Field(x => x.Name, nullable: true).Description("Name.");
        Field(x => x.Organization, nullable: true).Description("Company name.");
        Field(x => x.Phone, nullable: true).Description("Phone.");
        Field(x => x.PostalCode, nullable: true).Description("Postal code.");
        Field(x => x.RegionId, nullable: true).Description("Region id.");
        Field(x => x.RegionName, nullable: true).Description("Region name.");
        Field(x => x.Zip, nullable: true).Description("Zip.");
        Field(x => x.OuterId, nullable: true).Description("Outer id.");
        Field(x => x.Description, nullable: true).Description("Description.");
        Field<IntGraphType>(nameof(Address.AddressType)).Description("Address type.").Resolve(context => (int)context.Source.AddressType);
    }
}
