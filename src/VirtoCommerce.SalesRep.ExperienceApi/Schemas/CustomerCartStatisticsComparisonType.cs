using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerCartStatisticsComparisonType : ExtendableGraphType<CustomerCartStatisticsComparison>
{
    public static readonly string[] CartFigureFields =
    [
        "totalChange", "totalChangePercent",
        "countChange", "countChangePercent",
        "averageChange", "averageChangePercent",
    ];

    public CustomerCartStatisticsComparisonType(ICurrencyService currencyService)
    {
        Name = "CustomerCartStatisticsComparison";

        Field(x => x.SelectedItemQuantityChange, nullable: false).Description("Current selected-for-checkout quantity minus the previous one.");
        Field(x => x.SelectedItemQuantityChangePercent, nullable: true).Description("Percentage change of the selected-for-checkout quantity; null when the previous quantity is zero.");
        Field(x => x.UnselectedItemQuantityChange, nullable: false).Description("Current not-selected-for-checkout quantity minus the previous one.");
        Field(x => x.UnselectedItemQuantityChangePercent, nullable: true).Description("Percentage change of the not-selected-for-checkout quantity; null when the previous quantity is zero.");

        Field(x => x.CountChange, nullable: false).Description("Current contributing-cart count minus the previous one.");
        Field(x => x.CountChangePercent, nullable: true).Description("Percentage change of the count; null when the previous count is zero.");
        Field<NonNullGraphType<MoneyType>>("totalChange")
            .Description("Current goods subtotal minus the previous one.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.TotalChange));
        Field(x => x.TotalChangePercent, nullable: true).Description("Percentage change of the goods subtotal; null when the previous subtotal is zero.");
        Field<NonNullGraphType<MoneyType>>("averageChange")
            .Description("Current average minus the previous one.")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.AverageChange));
        Field(x => x.AverageChangePercent, nullable: true).Description("Percentage change of the average; null when the previous average is zero.");
    }
}
