using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using GraphQL.Types.Relay;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.Extensions;
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
    // with a subclass. That base lives in XOrder.Data (this module references .Core only) and it authorizes with
    // CanAccessOrderAuthorizationRequirement, which answers for the signed-in buyer rather than for the rep.
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

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrdersQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();
    }
}
