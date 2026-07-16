using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCartKindType : ExtendableGraphType<SalesRepCartKind>
{
    public SalesRepCartKindType()
    {
        Name = "SalesRepCartKind";

        Field(x => x.Name, nullable: false).Description("Stable kind id — send it back as the salesRepCustomerCartStatistics 'kinds' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the kind.");
    }
}
