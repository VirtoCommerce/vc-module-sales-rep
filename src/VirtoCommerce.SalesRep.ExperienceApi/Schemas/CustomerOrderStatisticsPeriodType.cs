using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerOrderStatisticsPeriodType : ExtendableGraphType<CustomerOrderStatisticsPeriod>
{
    public CustomerOrderStatisticsPeriodType(ICurrencyService currencyService)
    {
        Name = "CustomerOrderStatisticsPeriod";

        // total/average as Money (amount + formattedAmount + currency), mirroring SalesRepOrder.total. The service
        // already converted the amounts to the period's currency; resolve that code to a Currency for the requested
        // culture (GetAllCurrenciesAsync is cached, so this is safe per field without a DataLoader).
        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Sum of order totals in the range (amount, formatted amount and currency).")
            .ResolveAsync(context => ToMoneyAsync(currencyService, context, context.Source.Total));

        Field(x => x.Count, nullable: false).Description("Number of orders in the range.");

        Field<NonNullGraphType<MoneyType>>("average")
            .Description("Average order value in the range (amount, formatted amount and currency).")
            .ResolveAsync(context => ToMoneyAsync(currencyService, context, context.Source.Average));

        Field(x => x.LastOrderDate, nullable: true).Description("Date of the most recent order in the range.");
    }

    private static async Task<object> ToMoneyAsync(ICurrencyService currencyService, IResolveFieldContext<CustomerOrderStatisticsPeriod> context, decimal amount)
    {
        var currencies = await currencyService.GetAllCurrenciesAsync();
        var currency = currencies.GetCurrencyForLanguage(context.Source.CurrencyCode, context.GetCultureName());
        return new Money(amount, currency);
    }
}
