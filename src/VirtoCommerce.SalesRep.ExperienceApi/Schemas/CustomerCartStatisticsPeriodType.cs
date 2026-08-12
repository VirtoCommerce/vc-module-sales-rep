using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerCartStatisticsPeriodType : ExtendableGraphType<CustomerCartStatisticsPeriod>
{
    public static readonly string[] CartFigureFields = ["count", "total", "average", "warning"];

    public CustomerCartStatisticsPeriodType(ICurrencyService currencyService)
    {
        Name = "CustomerCartStatisticsPeriod";

        Field(x => x.SelectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items selected for checkout.");

        Field(x => x.UnselectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items NOT selected for checkout.");

        Field(x => x.Count, nullable: false)
            .Description("Number of distinct carts contributing to 'total'; a cart whose lines are all parked reports quantities but does not count.");

        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Goods subtotal of the lines picked for checkout in the range (list price less line discount, gifts excluded). Excludes shipping, taxes, fees and cart-level discounts.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Total));

        Field<NonNullGraphType<MoneyType>>("average")
            .Description("'total' divided by 'count'.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Average));

        Field(x => x.Warning, nullable: true)
            .Description("Non-null when 'count'/'total'/'average' exclude line items in an unconfigured currency; describes what was excluded. Item quantities are never affected.");
    }
}
