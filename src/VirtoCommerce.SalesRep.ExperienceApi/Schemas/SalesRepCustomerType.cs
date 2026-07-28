using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerType : ExtendableGraphType<SalesRepCustomer>
{
    public SalesRepCustomerType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ISalesRepCustomerOrderSearchService customerOrderSearchService,
        ISalesRepOrderResponseGroupParser responseGroupParser,
        ICustomerOrderStatisticsService statisticsService)
    {
        Name = "SalesRepCustomer";

        Field(x => x.OrganizationId, nullable: false).Description("Organization (customer) id.");
        Field(x => x.OrganizationName, nullable: true).Description("Organization (customer) name.");
        Field(x => x.AccountId, nullable: true).Description("External/display account id (the organization's OuterId); null when it has none.");
        Field(x => x.AccountType, nullable: true).Description("Account type — the organization's business category (e.g. \"Garden Center\").");
        Field(x => x.IconUrl, nullable: true).Description("URL of the organization's icon.");
        Field<SalesRepAddressType>("address")
            .Description("The organization's default address (structured; the storefront formats it, e.g. \"City, Region\").")
            .Resolve(context => context.Source.Address);

        Field<SalesRepOrderType>("lastOrder")
            .Description("The rep's most recent order for this customer (only orders the rep created).")
            .Resolve(context =>
            {
                var organizationId = context.Source.OrganizationId;
                if (string.IsNullOrEmpty(organizationId))
                {
                    return null;
                }

                var storeId = context.Source.StoreId;

                var salesRepUserId = context.GetCurrentUserId();

                var responseGroup = responseGroupParser.GetResponseGroup(
                    context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? []);

                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, SalesRepOrder>(
                    $"{nameof(SalesRepCustomerType)}.LastOrderByOrganizationId:{salesRepUserId}:{storeId}:{responseGroup}",
                    async organizationIds =>
                    {
                        var latestOrders = await customerOrderSearchService.GetLatestOrdersByOrganizationIdsAsync(organizationIds.ToList(), salesRepUserId, storeId, responseGroup);
                        return latestOrders.ToDictionary(kvp => kvp.Key, kvp => SalesRepOrder.FromOrder(kvp.Value), StringComparer.OrdinalIgnoreCase);
                    },
                    StringComparer.OrdinalIgnoreCase);

                return loader.LoadAsync(organizationId);
            });

        Field<CustomerOrderStatisticsPeriodType>("orderStatistics")
            .Description("The rep's own order statistics for this customer over a date range (YTD purchases, order count, average, first/last order). Omit both bounds for lifetime; request several aliased selections (e.g. ytd + lastYtd) to build the purchase columns.")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.FromArgument, "Inclusive lower bound on the order created date (null = no lower bound).")
            .Argument<DateTimeGraphType>(StatisticsFieldHelper.ToArgument, "Inclusive upper bound on the order created date (null = no upper bound).")
            .Argument<StringGraphType>("currencyCode", "Currency to convert the figures to (defaults to the store's default currency, then the platform primary).")
            .Resolve(context =>
            {
                var organizationId = context.Source.OrganizationId;
                if (string.IsNullOrEmpty(organizationId))
                {
                    return null;
                }

                var from = context.GetArgument<DateTime?>(StatisticsFieldHelper.FromArgument);
                var to = context.GetArgument<DateTime?>(StatisticsFieldHelper.ToArgument);

                var currencyCode = context.GetArgument<string>("currencyCode");
                if (string.IsNullOrEmpty(currencyCode))
                {
                    currencyCode = context.Source.CurrencyCode;
                }

                var salesRepUserId = context.GetCurrentUserId();
                var storeId = context.Source.StoreId;

                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, CustomerOrderStatisticsPeriod>(
                    $"{nameof(SalesRepCustomerType)}.OrderStatistics:{salesRepUserId}:{storeId}:{currencyCode}:{from:O}:{to:O}",
                    async organizationIds =>
                    {
                        var ids = organizationIds.ToList();

                        var criteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
                        criteria.OrganizationIds = ids.ToList();
                        criteria.CustomerId = salesRepUserId;
                        criteria.StoreId = storeId;
                        criteria.CurrencyCode = currencyCode;
                        criteria.FromDate = from;
                        criteria.ToDate = to;

                        var byOrganization = await statisticsService.GetStatisticsByOrganizationAsync(criteria);

                        return ids.ToDictionary(
                            id => id,
                            id => byOrganization.TryGetValue(id, out var period) ? period : StatisticsFieldHelper.EmptyPeriod<CustomerOrderStatisticsPeriod>(p => p.CurrencyCode = currencyCode),
                            StringComparer.OrdinalIgnoreCase);
                    },
                    StringComparer.OrdinalIgnoreCase);

                return loader.LoadAsync(organizationId);
            });
    }
}
