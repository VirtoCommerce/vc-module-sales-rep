using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerCartStatisticsPeriodType : ExtendableGraphType<CustomerCartStatisticsPeriod>
{
    public CustomerCartStatisticsPeriodType(ICurrencyService currencyService)
    {
        Name = "CustomerCartStatisticsPeriod";

        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Sum of cart totals in the range (amount, formatted amount and currency).")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Total));

        Field(x => x.Count, nullable: false).Description("Number of carts in the range.");

        Field(x => x.SelectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items selected for checkout (the primary widget metric, e.g. 'Active carts · items'). Unlike the cart figures, the range bounds each LINE ITEM's modified date, so a cart created earlier still contributes the items touched inside the range.");

        Field(x => x.UnselectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items NOT selected for checkout, over the same line-item modified-date range as 'selectedItemQuantity'.");

        Field<NonNullGraphType<MoneyType>>("average")
            .Description("Average cart value in the range (amount, formatted amount and currency).")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Average));

        Field(x => x.LastCartDate, nullable: true).Description("Date of the most recent cart in the range.");
        Field(x => x.Warning, nullable: true).Description("Non-null when the figures are partial because some carts were in an unconfigured currency and could not be converted; describes what was excluded.");
    }
}
