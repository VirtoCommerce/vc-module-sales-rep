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

                // The store is uniform across a page (from the query's storeId argument); fold it into the loader
                // key so orders stay scoped to the caller's store and different stores never share a batch.
                var storeId = context.Source.StoreId;

                // Only the current sales rep's own orders count as a customer's "last order" — the rep's user id is
                // the order's CustomerId (as X-Order scopes its "my orders" list). Fold it into the loader key so
                // different callers never share a batch.
                var salesRepUserId = context.GetCurrentUserId();

                // Load only the order data the caller selected under lastOrder (e.g. skip line items unless
                // itemsCount was requested). The selection is uniform across the page, so fold the resulting
                // response group into the loader key too.
                var responseGroup = responseGroupParser.GetResponseGroup(
                    context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? []);

                // Batch every customer row on the page into one service call (which runs one bounded search
                // per organization) instead of a resolver-level order query per row.
                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, SalesRepOrder>(
                    $"{nameof(SalesRepCustomerType)}.LastOrderByOrganizationId:{salesRepUserId}:{storeId}:{responseGroup}",
                    async organizationIds =>
                    {
                        var latestOrders = await customerOrderSearchService.GetLatestOrdersByOrganizationIdsAsync(organizationIds.ToList(), salesRepUserId, storeId, responseGroup);
                        // The result dictionary matches organization ids case-insensitively (as the search service does).
                        return latestOrders.ToDictionary(kvp => kvp.Key, kvp => SalesRepOrder.FromOrder(kvp.Value), StringComparer.OrdinalIgnoreCase);
                    },
                    // Dedupe the batch keys with the same comparer as the result dictionary, so two ids differing only
                    // in case collapse to one bucket rather than surviving as distinct keys and colliding on ToDictionary.
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

                // Only the rep's own orders (their user id is the order's CustomerId), scoped to the caller's store.
                var salesRepUserId = context.GetCurrentUserId();
                var storeId = context.Source.StoreId;

                // One grouped aggregate for the whole page per distinct (range, currency) — the range/currency go in
                // the loader key, the organization ids are the batch, mirroring the counts widget's loader.
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

                        // Every requested id needs an entry; organizations with no orders in range → an empty
                        // (zeroed) period so the row still renders.
                        return ids.ToDictionary(
                            id => id,
                            id => byOrganization.TryGetValue(id, out var period) ? period : StatisticsFieldHelper.EmptyPeriod<CustomerOrderStatisticsPeriod>(p => p.CurrencyCode = currencyCode),
                            StringComparer.OrdinalIgnoreCase);
                    },
                    // Dedupe the batch keys case-insensitively, matching both the result dictionary above and the
                    // service's OrdinalIgnoreCase grouping, so cased-duplicate ids can't collide on ToDictionary.
                    StringComparer.OrdinalIgnoreCase);

                return loader.LoadAsync(organizationId);
            });
    }
}
