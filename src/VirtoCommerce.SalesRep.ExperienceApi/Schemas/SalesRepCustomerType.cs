using System;
using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerType : ExtendableGraphType<SalesRepCustomer>
{
    public SalesRepCustomerType(
        IDataLoaderContextAccessor dataLoaderContextAccessor,
        ISalesRepCustomerOrderSearchService customerOrderSearchService)
    {
        Name = "SalesRepCustomer";

        Field(x => x.OrganizationId, nullable: false).Description("Organization (customer) id.");
        Field(x => x.OrganizationName, nullable: true).Description("Organization (customer) name.");

        Field<SalesRepLastOrderType>("lastOrder")
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

                // Collapse every customer row on the page into a single grouped order query per request
                // (instead of one query per row).
                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, SalesRepLastOrder>(
                    $"{nameof(SalesRepCustomerType)}.LastOrderByOrganizationId:{storeId}",
                    async organizationIds =>
                    {
                        var latestOrders = await customerOrderSearchService.GetLatestOrdersByOrganizationIdsAsync(organizationIds.ToList(), storeId);
                        // Keep the loader's key comparer aligned with the service's OrdinalIgnoreCase dictionary.
                        return latestOrders.ToDictionary(kvp => kvp.Key, kvp => SalesRepLastOrder.FromOrder(kvp.Value), StringComparer.OrdinalIgnoreCase);
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
