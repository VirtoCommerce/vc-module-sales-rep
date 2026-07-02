using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepContactType : ExtendableGraphType<SalesRepContact>
{
    public SalesRepContactType()
    {
        Name = "SalesRepContact";

        Field(x => x.Id, nullable: false).Description("Contact (member) id of the Sales Rep.");
        Field(x => x.FirstName, nullable: true).Description("First name.");
        Field(x => x.LastName, nullable: true).Description("Last name.");
        Field(x => x.MiddleName, nullable: true).Description("Middle name.");
        Field(x => x.FullName, nullable: true).Description("Full name.");
        Field(x => x.Name, nullable: true).Description("Display name.");
        Field(x => x.About, nullable: true).Description("About the Sales Rep.");
        Field(x => x.PhotoUrl, nullable: true).Description("Photo URL.");

        Field<ListGraphType<StringGraphType>>("emails")
            .Description("Email addresses to contact the Sales Rep.")
            .Resolve(context => context.Source.Emails);

        Field<ListGraphType<StringGraphType>>("phones")
            .Description("Phone numbers to contact the Sales Rep.")
            .Resolve(context => context.Source.Phones);
    }
}
