using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerSortRuleType : ExtendableGraphType<SalesRepCustomerSortRule>
{
    public SalesRepCustomerSortRuleType()
    {
        Name = "SalesRepCustomerSortRule";

        Field(x => x.Name, nullable: false).Description("Stable sort-rule id — send it back as the salesRepCustomers 'sort' argument (optionally suffixed ':asc'/':desc').");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the ordering.");
        Field<NonNullGraphType<StringGraphType>>("defaultDirection")
            .Description("Direction applied when the 'sort' argument carries no direction suffix: 'asc' or 'desc'.")
            .Resolve(context => context.Source.DefaultDirection == SortDirection.Descending ? "desc" : "asc");
        Field(x => x.SupportsDirection, nullable: false).Description("Whether the client may choose the direction (e.g. 'name:desc'); false = a ':asc'/':desc' opposite of the default is rejected.");
    }
}
