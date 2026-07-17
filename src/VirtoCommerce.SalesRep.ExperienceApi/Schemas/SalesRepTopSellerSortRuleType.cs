using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTopSellerSortRuleType : ExtendableGraphType<SalesRepTopSellerSortRule>
{
    public SalesRepTopSellerSortRuleType()
    {
        Name = "SalesRepTopSellerSortRule";

        Field(x => x.Name, nullable: false).Description("Stable sort-rule id — send it back as the salesRepTopSellers 'sort' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the ordering.");
    }
}
