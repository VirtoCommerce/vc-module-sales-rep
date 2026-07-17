using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerSortRuleType : ExtendableGraphType<SalesRepCustomerSortRule>
{
    public SalesRepCustomerSortRuleType()
    {
        Name = "SalesRepCustomerSortRule";

        Field(x => x.Name, nullable: false).Description("Stable sort-rule id — send it back as the salesRepCustomers 'sort' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the ordering.");
    }
}
