using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderType : ExtendableGraphType<SalesRepOrder>
{
    public SalesRepOrderType(
        ILocalizableSettingService localizableSettingService,
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        IMemberService memberService,
        ICurrencyService currencyService)
    {
        Name = "SalesRepOrder";

        Field(x => x.Id, nullable: false).Description("Order id.");
        Field(x => x.Number, nullable: true).Description("Human-readable order number.");
        Field(x => x.OrganizationId, nullable: true).Description("Organization (customer) id the order belongs to.");
        Field(x => x.CreatedDate, nullable: false).Description("Date the order was placed.");
        LocalizedField(x => x.Status, OrderSettings.OrderStatus, localizableSettingService, nullable: true);
        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Order grand total (amount, formatted amount and currency).")
            .ResolveAsync(async context =>
            {
                var currencies = await currencyService.GetAllCurrenciesAsync();
                var currency = currencies.GetCurrencyForLanguage(context.Source.Currency, context.GetCultureName());
                return new Money(context.Source.Total, currency);
            });
        Field(x => x.ItemsCount, nullable: false).Description("Number of distinct line items in the order.");
        Field(x => x.ItemsQuantity, nullable: false).Description("Total number of units in the order (sum of line-item quantities) — the \"N units\" figure.");

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
                    $"{nameof(SalesRepOrderType)}.OrganizationNameById",
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
