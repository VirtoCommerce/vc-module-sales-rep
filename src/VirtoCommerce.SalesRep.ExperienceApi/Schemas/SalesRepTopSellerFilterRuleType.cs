using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTopSellerFilterRuleType : ExtendableGraphType<SalesRepTopSellerFilterRule>
{
    public SalesRepTopSellerFilterRuleType()
    {
        Name = "SalesRepTopSellerFilterRule";

        Field(x => x.Name, nullable: false).Description("Stable rule id (a top-level category id) — send it back as the salesRepTopSellers 'filter' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized category label.");
    }
}
