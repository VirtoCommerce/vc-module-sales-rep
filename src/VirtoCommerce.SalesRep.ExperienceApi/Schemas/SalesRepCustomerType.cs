using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using VirtoCommerce.SalesRep.Core.Services;
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
        ISalesRepOrderResponseGroupParser responseGroupParser)
    {
        Name = "SalesRepCustomer";

        Field(x => x.OrganizationId, nullable: false).Description("Organization (customer) id.");
        Field(x => x.OrganizationName, nullable: true).Description("Organization (customer) name.");

        Field<SalesRepOrderType>("lastOrder")
            .Description("The customer's most recent order.")
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
                        // Keep the loader's key comparer aligned with the service's OrdinalIgnoreCase dictionary.
                        return latestOrders.ToDictionary(kvp => kvp.Key, kvp => SalesRepOrder.FromOrder(kvp.Value), StringComparer.OrdinalIgnoreCase);
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
