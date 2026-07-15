using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerOrderStatisticsComparisonType : ExtendableGraphType<CustomerOrderStatisticsComparison>
{
    public CustomerOrderStatisticsComparisonType(ICurrencyService currencyService)
    {
        Name = "CustomerOrderStatisticsComparison";

        // Monetary changes as Money (amount + formattedAmount + currency); the percentages and count change stay scalars.
        Field<NonNullGraphType<MoneyType>>("totalChange")
            .Description("Current total minus previous total (amount, formatted amount and currency).")
            .ResolveAsync(context => ToMoneyAsync(currencyService, context, context.Source.TotalChange));
        Field(x => x.TotalChangePercent, nullable: true).Description("Percentage change of total; null when the previous total is zero.");
        Field(x => x.CountChange, nullable: false).Description("Current count minus previous count.");
        Field(x => x.CountChangePercent, nullable: true).Description("Percentage change of count; null when the previous count is zero.");
        Field<NonNullGraphType<MoneyType>>("averageChange")
            .Description("Current average minus previous average (amount, formatted amount and currency).")
            .ResolveAsync(context => ToMoneyAsync(currencyService, context, context.Source.AverageChange));
        Field(x => x.AverageChangePercent, nullable: true).Description("Percentage change of average; null when the previous average is zero.");
    }

    private static async Task<object> ToMoneyAsync(ICurrencyService currencyService, IResolveFieldContext<CustomerOrderStatisticsComparison> context, decimal amount)
    {
        var currencies = await currencyService.GetAllCurrenciesAsync();
        var currency = currencies.GetCurrencyForLanguage(context.Source.CurrencyCode, context.GetCultureName());
        return new Money(amount, currency);
    }
}
