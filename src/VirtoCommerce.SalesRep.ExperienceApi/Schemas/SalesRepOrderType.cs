using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepOrderType : ExtendableGraphType<SalesRepOrder>
{
    public SalesRepOrderType(
        ILocalizableSettingService localizableSettingService,
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        IMemberService memberService)
    {
        Name = "SalesRepOrder";

        Field(x => x.Id, nullable: false).Description("Order id.");
        Field(x => x.Number, nullable: true).Description("Human-readable order number.");
        Field(x => x.CustomerId, nullable: true).Description("Customer (organization) id the order belongs to.");
        Field(x => x.CreatedDate, nullable: false).Description("Date the order was placed.");
        // Adds `status` (raw) + `statusDisplayValue` (localized from the Order.Status dictionary; culture from context).
        LocalizedField(x => x.Status, OrderSettings.OrderStatus, localizableSettingService, nullable: true);
        Field(x => x.Total, nullable: false).Description("Order grand total.");
        Field(x => x.Currency, nullable: true).Description("Order currency code (the currency in which the order was submitted).");
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
