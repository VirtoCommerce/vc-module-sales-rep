using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderType : ExtendableGraphType<SalesRepOrder>
{
    public SalesRepOrderType()
    {
        Name = "SalesRepOrder";

        Field(x => x.Id, nullable: false).Description("Order id.");
        Field(x => x.Number, nullable: true).Description("Human-readable order number.");
        Field(x => x.CreatedDate, nullable: false).Description("Date the order was placed.");
        Field(x => x.Status, nullable: true).Description("Raw order status.");
        Field(x => x.StatusLocalized, nullable: true).Description("Localized label of the order's raw status.");
        Field(x => x.Total, nullable: false).Description("Order grand total.");
        Field(x => x.Currency, nullable: true).Description("Order currency code (the currency in which the order was submitted).");
        Field(x => x.ItemsCount, nullable: false).Description("Number of line items in the order.");
    }
}
