using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerCartStatisticsPeriodType : ExtendableGraphType<CustomerCartStatisticsPeriod>
{
    /// <summary>
    /// Fields that cost the extra COUNT DISTINCT and currency conversion, so the resolver only aggregates them when
    /// one of them is selected (see <see cref="CustomerCartStatisticsType"/>).
    /// </summary>
    public static readonly string[] CartFigureFields = ["count", "total", "average", "warning"];

    public CustomerCartStatisticsPeriodType(ICurrencyService currencyService)
    {
        Name = "CustomerCartStatisticsPeriod";

        Field(x => x.SelectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items selected for checkout (the primary widget metric, e.g. 'Active carts · items').");

        Field(x => x.UnselectedItemQuantity, nullable: false)
            .Description("Summed quantity of the line items NOT selected for checkout.");

        Field(x => x.Count, nullable: false)
            .Description("Number of distinct carts contributing to 'total' — those holding at least one line picked for checkout in the range, gifts excluded. A cart whose lines are all parked therefore reports quantities but does not count.");

        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Goods subtotal of the lines in the range: list price less line discount, over the lines selected for checkout, gifts excluded. Shipping, taxes, fees and cart-level discounts are NOT included, so this is the carts' subtotal rather than their grand total. Read from the persisted line prices, which the platform only refreshes on a full-cart operation.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Total));

        Field<NonNullGraphType<MoneyType>>("average")
            .Description("'total' divided by 'count' — the average goods subtotal per contributing cart.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Average));

        Field(x => x.Warning, nullable: true)
            .Description("Non-null when 'count'/'total'/'average' are partial because some line items were in an unconfigured currency and could not be converted; describes what was excluded. The item quantities are never affected — a quantity needs no exchange rate.");
    }
}
