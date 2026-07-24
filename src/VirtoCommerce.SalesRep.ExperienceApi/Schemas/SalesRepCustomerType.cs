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
                    });

                return loader.LoadAsync(organizationId);
            });
    }
}
