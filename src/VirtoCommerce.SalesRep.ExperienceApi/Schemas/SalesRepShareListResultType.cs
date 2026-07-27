using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepShareListResultType : ExtendableGraphType<SalesRepShareListResult>
{
    public SalesRepShareListResultType()
    {
        Name = "SalesRepShareListResult";

        Field(x => x.Succeeded, nullable: false)
            .Description("True when the list was published (the Customer scope was applied).");
        Field(x => x.ListId, nullable: true)
            .Description("The shared list id.");
        Field(x => x.SharingKey, nullable: true)
            .Description("Stable sharing key — the /shared-list/{key} token delivered to customers.");
        Field(x => x.SharingUrl, nullable: true)
            .Description("Absolute shared-list URL delivered to the customers.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(SalesRepShareListResult.SharedWithOrganizationIds))
            .Description("Customer organizations the list is now shared with.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<StringGraphType>>>>(nameof(SalesRepShareListResult.Warnings))
            .Description("Stable outcome codes for any notification channel that did not deliver (empty on full success).");
    }
}
