using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerFilterRuleType : ExtendableGraphType<SalesRepCustomerFilterRule>
{
    public SalesRepCustomerFilterRuleType()
    {
        Name = "SalesRepCustomerFilterRule";

        Field(x => x.Name, nullable: false).Description("Stable segment id — send it back in the salesRepCustomers / salesRepCustomerCounts 'filter' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the segment.");
    }
}
