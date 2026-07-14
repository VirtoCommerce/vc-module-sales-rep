using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderStatusType : ExtendableGraphType<SalesRepOrderStatus>
{
    public SalesRepOrderStatusType()
    {
        Name = "SalesRepOrderStatus";

        Field(x => x.Name, nullable: false).Description("Stable status id — send it back as the salesRepOrders 'status' argument.");
        Field(x => x.LocalizedName, nullable: true).Description("Localized label for the status tab / badge.");
    }
}
