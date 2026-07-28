using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderSortRuleType : ExtendableGraphType<SalesRepOrderSortRule>
{
    public SalesRepOrderSortRuleType()
    {
        Name = "SalesRepOrderSortRule";

        Field(x => x.Name, nullable: false).Description("Stable sort-rule id — send it back as the salesRepOrders 'sort' argument (optionally suffixed ':asc'/':desc').");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the ordering.");
        Field<NonNullGraphType<StringGraphType>>("defaultDirection")
            .Description("Direction applied when the 'sort' argument carries no direction suffix: 'asc' or 'desc'.")
            .Resolve(context => context.Source.DefaultDirection.ToToken());
        Field(x => x.SupportsDirection, nullable: false).Description("Whether the client may choose the direction (e.g. 'total:asc'); false = a ':asc'/':desc' opposite of the default is rejected.");
    }
}
