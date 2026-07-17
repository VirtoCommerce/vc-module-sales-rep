using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderSortRuleType : ExtendableGraphType<SalesRepOrderSortRule>
{
    public SalesRepOrderSortRuleType()
    {
        Name = "SalesRepOrderSortRule";

        Field(x => x.Name, nullable: false).Description("Stable sort-rule id — send it back as the salesRepOrders 'sort' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the ordering.");
    }
}
