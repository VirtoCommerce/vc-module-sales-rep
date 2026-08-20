using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrderQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerOrderQuery, CustomerOrderAggregate, CustomerOrderType>
{
    protected override string Name => "salesRepCustomerOrder";

    private readonly ICurrencyService _currencyService;

    public SalesRepCustomerOrderQueryBuilder(IAuthorizationService authorizationService, ICurrencyService currencyService)
        : base(authorizationService)
    {
        _currencyService = currencyService;
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrderQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        var currencies = await _currencyService.GetAllCurrenciesAsync();
        context.SetCurrencies(currencies, request.CultureName);
    }

    protected override Task AfterMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrderQuery request, CustomerOrderAggregate response)
    {
        if (response != null)
        {
            context.SetExpandedObjectGraph(response);
        }

        return Task.CompletedTask;
    }
}
