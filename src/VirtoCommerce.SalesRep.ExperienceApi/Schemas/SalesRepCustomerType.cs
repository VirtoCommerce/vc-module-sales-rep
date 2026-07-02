using System.Linq;
using GraphQL;
using GraphQL.DataLoader;
using VirtoCommerce.OrdersModule.Core.Model;
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

                // Collapse every customer row on the page into a single grouped order query per request
                // (instead of one query per row).
                var loader = dataLoaderContextAccessor.Context.GetOrAddBatchLoader<string, SalesRepLastOrder>(
                    $"{nameof(SalesRepCustomerType)}.LastOrderByOrganizationId",
                    async organizationIds =>
                    {
                        var latestOrders = await customerOrderSearchService.GetLatestOrdersByOrganizationIdsAsync(organizationIds.ToList());
                        return latestOrders.ToDictionary(kvp => kvp.Key, kvp => MapLastOrder(kvp.Value));
                    });

                return loader.LoadAsync(organizationId);
            });
    }

    private static SalesRepLastOrder MapLastOrder(CustomerOrder order)
    {
        return new SalesRepLastOrder
        {
            Id = order.Id,
            Number = order.Number,
            CreatedDate = order.CreatedDate,
            Status = order.Status,
            Total = order.Total,
            Currency = order.Currency,
        };
    }
}
