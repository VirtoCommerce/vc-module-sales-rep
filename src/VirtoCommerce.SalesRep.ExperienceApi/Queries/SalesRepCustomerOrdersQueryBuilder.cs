using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using GraphQL.Types.Relay;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Queries;
using VirtoCommerce.XOrder.Core.Schemas;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrdersQueryBuilder : SalesRepOrderQueryBuilder<SalesRepCustomerOrdersQuery, SearchOrderResponse>
{
    protected override string Name => "salesRepCustomerOrders";

    public SalesRepCustomerOrdersQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    // IMPORTANT: repeats X-Order's BaseSearchOrderQueryBuilder.GetFieldType on purpose — do not subclass it.
    // That base authorizes with CanAccessOrderAuthorizationRequirement, which grants on order ownership or
    // membership of the buying organization; a rep has neither, so inheriting it would return nothing. Keep in
    // step with the base if X-Order changes the connection shape.
    protected override FieldType GetFieldType()
    {
        var builder = GraphTypeExtensionHelper
            .CreateConnection<CustomerOrderType, EdgeType<CustomerOrderType>, CustomerOrderConnectionType<CustomerOrderType>, object>(Name)
            .PageSize(Connections.DefaultPageSize);

        ConfigureArguments(builder.FieldType);

        builder.ResolveAsync(async context =>
        {
            var (query, response) = await Resolve(context);

            return new CustomerOrderConnection<CustomerOrderAggregate>(response.Results, query.Skip, query.Take, response.TotalCount)
            {
                Facets = response.Facets,
            };
        });

        return builder.FieldType;
    }

    protected override string GetCultureName(SalesRepCustomerOrdersQuery request) => request.CultureName;

    protected override IEnumerable<CustomerOrderAggregate> GetOrderAggregates(SearchOrderResponse response) => response.Results;
}
