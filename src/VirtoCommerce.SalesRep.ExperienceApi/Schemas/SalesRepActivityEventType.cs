using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
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
            .ResolveAsync(async context =>
            {
                if (string.IsNullOrEmpty(context.Source.OrderCurrency) || context.Source.OrderTotal == null)
                {
                    return null;
                }

                var currencies = await currencyService.GetAllCurrenciesAsync();
                var currency = currencies.GetCurrencyForLanguage(context.Source.OrderCurrency, context.GetCultureName());
                return new Money(context.Source.OrderTotal.Value, currency);
            });

        Field(x => x.SearchTerm, nullable: true).Description("Searched phrase (search rows).");

        Field(x => x.ProductId, nullable: true).Description("Resolved product id (productView rows; null when the code no longer matches a product).");
        Field(x => x.ProductCode, nullable: true).Description("Product code as tracked by analytics (productView rows).");
        Field(x => x.ProductName, nullable: true).Description("Product name (productView rows; resolved from the catalog, falling back to the tracked name).");
        Field(x => x.ProductImageUrl, nullable: true).Description("Product image URL (productView rows; null when unresolved).");

        Field<StringGraphType>("organizationName")
            .Description("Organization (customer) name.")
            .Resolve(context =>
            {
                if (!string.IsNullOrEmpty(context.Source.OrganizationName))
                {
                    return context.Source.OrganizationName;
                }

                var organizationId = context.Source.OrganizationId;
                if (string.IsNullOrEmpty(organizationId))
                {
                    return null;
                }

                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, string>(
                    $"{nameof(SalesRepActivityEventType)}.OrganizationNameById",
                    async organizationIds =>
                    {
                        var organizations = await memberService.GetByIdsAsync(
                            organizationIds.ToArray(),
                            nameof(MemberResponseGroup.Default),
                            [nameof(Organization)]);

                        return organizations.ToDictionary(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
