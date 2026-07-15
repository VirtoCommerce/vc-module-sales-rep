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
        Field(x => x.CustomerId, nullable: true).Description("Customer (organization) id the order belongs to.");
        Field(x => x.CreatedDate, nullable: false).Description("Date the order was placed.");
        // Adds `status` (raw) + `statusDisplayValue` (localized from the Order.Status dictionary; culture from context).
        LocalizedField(x => x.Status, OrderSettings.OrderStatus, localizableSettingService, nullable: true);
        // Grand total as Money so clients get amount + formattedAmount (+ the currency object). The order stores a
        // currency code; resolve it to the full Currency for the requested culture — GetAllCurrenciesAsync is cached,
        // so this is safe per row without a DataLoader. Culture comes from the query's cultureName argument (copied
        // to the user context by SalesRepOrdersQueryBuilder); when absent, formatting falls back to the invariant culture.
        Field<NonNullGraphType<MoneyType>>("total")
            .Description("Order grand total (amount, formatted amount and currency).")
            .ResolveAsync(async context =>
            {
                var currencies = await currencyService.GetAllCurrenciesAsync();
                var currency = currencies.GetCurrencyForLanguage(context.Source.Currency, context.GetCultureName());
                return new Money(context.Source.Total, currency);
            });
        Field(x => x.ItemsCount, nullable: false).Description("Number of line items in the order.");

        // Customer (organization) name — the value denormalized on the order when present; otherwise resolved from
        // the organization id, batched per request (one member query for the whole page, only for the orders that
        // are missing it) so the cross-customer dashboard doesn't do N lookups.
        Field<StringGraphType>("customerName")
            .Description("Customer (organization) name.")
            .Resolve(context =>
            {
                if (!string.IsNullOrEmpty(context.Source.CustomerName))
                {
                    return context.Source.CustomerName;
                }

                var organizationId = context.Source.CustomerId;
                if (string.IsNullOrEmpty(organizationId))
                {
                    return null;
                }

                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, string>(
                    $"{nameof(SalesRepOrderType)}.CustomerNameByOrganizationId",
                    async organizationIds =>
                    {
                        var organizations = await memberService.GetByIdsAsync(
                            organizationIds.ToArray(),
                            MemberResponseGroup.Default.ToString(),
                            [nameof(Organization)]);

                        return organizations.ToDictionary(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
