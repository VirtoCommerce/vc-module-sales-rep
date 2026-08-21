using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using GraphQL.Types.Relay;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Queries;
using VirtoCommerce.XOrder.Core.Schemas;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrdersQueryBuilder : SalesRepOrderQueryBuilder<SalesRepCustomerOrdersQuery, SearchOrderResponse>
{
    protected override string Name => "salesRepCustomerOrders";

    public SalesRepCustomerOrdersQueryBuilder(IAuthorizationService authorizationService, ICurrencyService currencyService)
        : base(authorizationService, currencyService)
    {
    }

    // IMPORTANT: this repeats X-Order's BaseSearchOrderQueryBuilder.GetFieldType on purpose — do not replace it
    // with a subclass. That base authorizes with CanAccessOrderAuthorizationRequirement, whose handler grants on
    // "you placed this order" or "your contact belongs to the buying organization" (plus an administrator
    // bypass). A rep is none of those for a customer's order — they serve the organization rather than belong
    // to it — so inheriting that gate would return nothing. This endpoint answers to the module's own gate
    // instead: an unlocked OrganizationMembership carrying sales-rep:access, scoping the results to exactly
    // those organizations, and no administrator bypass. The half of the base worth keeping is its account-state
    // check, which SalesRepQueryBuilder now applies module-wide.
    // Keep in step with the base if X-Order changes the connection shape.
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
