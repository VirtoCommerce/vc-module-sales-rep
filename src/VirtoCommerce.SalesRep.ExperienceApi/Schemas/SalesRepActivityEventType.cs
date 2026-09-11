using System.Threading.Tasks;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepActivityEventType : ExtendableGraphType<SalesRepActivityEvent>
{
    public SalesRepActivityEventType(
        ILocalizableSettingService localizableSettingService,
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        IMemberService memberService,
        ICurrencyService currencyService)
    {
        Name = "SalesRepActivityEvent";

        Field(x => x.Category, nullable: false).Description("Activity category: orders | customers | searches | productViews | logins.");
        Field(x => x.Type, nullable: false).Description("Activity type: orderPlaced | customerAssigned | search | productView | login.");
        Field(x => x.OccurredAt, nullable: false).Description("When the activity happened (UTC). Hour-precision rows carry the hour-bucket start.");
        Field(x => x.Precision, nullable: false).Description("Timestamp precision: exact | hour.");
        Field<IntGraphType>("count")
            .Description("Number of occurrences this row aggregates (>1 for analytics hour-buckets).")
            .Resolve(context => context.Source.Count);
        Field(x => x.OrganizationId, nullable: true).Description("Organization (customer) id the activity belongs to.");

        Field(x => x.OrderId, nullable: true).Description("Order id (orderPlaced rows).");
        Field(x => x.OrderNumber, nullable: true).Description("Human-readable order number (orderPlaced rows).");
        LocalizedField(x => x.OrderStatus, OrderSettings.OrderStatus, localizableSettingService, nullable: true);
        Field<MoneyType>("orderTotal")
            .Description("Order grand total (orderPlaced rows).")
            .ResolveAsync(context =>
            {
                if (string.IsNullOrEmpty(context.Source.OrderCurrency) || context.Source.OrderTotal == null)
                {
                    return Task.FromResult<object>(null);
                }

                return StatisticsFieldHelper.ToMoneyAsync(currencyService, context.Source.OrderCurrency, context.GetCultureName(), context.Source.OrderTotal.Value);
            });

        Field(x => x.SearchTerm, nullable: true).Description("Searched phrase (search rows).");

        Field(x => x.ProductId, nullable: true).Description("Resolved product id (productView rows; null when the code no longer matches a product).");
        Field(x => x.ProductCode, nullable: true).Description("Product code as tracked by analytics (productView rows).");
        Field(x => x.ProductName, nullable: true).Description("Product name (productView rows; resolved from the catalog, falling back to the tracked name).");
        Field(x => x.ProductImageUrl, nullable: true).Description("Product image URL (productView rows; null when unresolved).");

        this.AddOrganizationNameField(dataLoaderContextAccessor, memberService, x => x.OrganizationName, x => x.OrganizationId);
    }
}
