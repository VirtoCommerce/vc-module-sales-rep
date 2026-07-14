using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderType : ExtendableGraphType<SalesRepOrder>
{
    public SalesRepOrderType(ILocalizableSettingService localizableSettingService)
    {
        Name = "SalesRepOrder";

        Field(x => x.Id, nullable: false).Description("Order id.");
        Field(x => x.Number, nullable: true).Description("Human-readable order number.");
        Field(x => x.CreatedDate, nullable: false).Description("Date the order was placed.");
        // Adds `status` (raw) + `statusDisplayValue` (localized from the Order.Status dictionary; culture from context).
        LocalizedField(x => x.Status, OrderSettings.OrderStatus, localizableSettingService, nullable: true);
        Field(x => x.Total, nullable: false).Description("Order grand total.");
        Field(x => x.Currency, nullable: true).Description("Order currency code (the currency in which the order was submitted).");
        Field(x => x.ItemsCount, nullable: false).Description("Number of line items in the order.");
    }
}
