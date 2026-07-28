using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTopSellerType : ExtendableGraphType<SalesRepTopSeller>
{
    public SalesRepTopSellerType(ICurrencyService currencyService)
    {
        Name = "SalesRepTopSeller";

        Field(x => x.Rank, nullable: false).Description("1-based rank in the list (by the selected metric).");
        Field(x => x.ProductId, nullable: false).Description("Product id the sales were aggregated by.");
        Field(x => x.Name, nullable: true).Description("Product name (from the line-item snapshot).");
        Field(x => x.Sku, nullable: true).Description("Product SKU (from the line-item snapshot).");
        Field(x => x.ImageUrl, nullable: true).Description("Product image URL (from the line-item snapshot).");
        Field(x => x.CategoryId, nullable: true).Description("Category id (from the line-item snapshot).");
        Field(x => x.Units, nullable: false).Description("Total units sold (sum of line-item quantities).");

        Field<NonNullGraphType<MoneyType>>("revenue")
            .Description("Total revenue — sum of quantity × unit price (amount, formatted amount and currency).")
            .ResolveAsync(context => StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.CurrencyCode, context.GetCultureName(), context.Source.Revenue));

        Field(x => x.Warning, nullable: true).Description("Non-null when Revenue is partial because some of this product's sales were in an unconfigured currency and could not be converted; describes what was excluded.");
    }
}
