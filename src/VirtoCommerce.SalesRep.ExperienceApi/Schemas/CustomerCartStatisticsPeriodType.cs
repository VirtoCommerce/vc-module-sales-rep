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

        Field(x => x.Count, nullable: false).Description("Number of carts in the range (the primary widget metric, e.g. 'Active Projects').");

        Field<NonNullGraphType<MoneyType>>("average")
            .Description("Average cart value in the range (amount, formatted amount and currency).")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Average));

        Field(x => x.LastCartDate, nullable: true).Description("Date of the most recent cart in the range.");
    }
}
