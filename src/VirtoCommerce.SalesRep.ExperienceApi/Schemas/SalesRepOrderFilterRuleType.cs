using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderFilterRuleType : ExtendableGraphType<SalesRepOrderFilterRule>
{
    public SalesRepOrderFilterRuleType()
    {
        Name = "SalesRepOrderFilterRule";

        Field(x => x.Name, nullable: false).Description("Stable status id — send it back as the salesRepOrders 'status' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the status.");
    }
}
