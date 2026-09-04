using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTaskFilterRuleType : ExtendableGraphType<SalesRepTaskFilterRule>
{
    public SalesRepTaskFilterRuleType()
    {
        Name = "SalesRepTaskFilterRule";

        Field(x => x.Name, nullable: false).Description("Stable rule id — send it back in the salesRepTasks 'filter' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the rule.");
    }
}
