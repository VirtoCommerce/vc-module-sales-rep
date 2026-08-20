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

public class SalesRepCustomerOrdersQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerOrdersQuery, SearchOrderResponse, CustomerOrderType>
{
    protected override string Name => "salesRepCustomerOrders";

    private readonly ICurrencyService _currencyService;

    public SalesRepCustomerOrdersQueryBuilder(IAuthorizationService authorizationService, ICurrencyService currencyService)
        : base(authorizationService)
    {
        _currencyService = currencyService;
    }

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

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrdersQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();

        var currencies = await _currencyService.GetAllCurrenciesAsync();
        context.SetCurrencies(currencies, request.CultureName);
    }

    protected override Task AfterMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrdersQuery request, SearchOrderResponse response)
    {
        foreach (var aggregate in response.Results)
        {
            context.SetExpandedObjectGraph(aggregate);
        }

        return Task.CompletedTask;
    }
}
