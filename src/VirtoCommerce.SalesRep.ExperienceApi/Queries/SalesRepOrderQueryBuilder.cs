using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepOrderQueryBuilder<TQuery, TResult> : SalesRepQueryBuilder<TQuery, TResult, CustomerOrderType>
    where TQuery : IQuery<TResult>, IExtendableQuery, IHasArguments
{
    protected SalesRepOrderQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected abstract string GetCultureName(TQuery request);

    protected abstract IEnumerable<CustomerOrderAggregate> GetOrderAggregates(TResult response);

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        // CustomerOrderType's localized fields take no cultureName argument; they read the user context.
        context.CopyArgumentsToUserContext();

        var currencies = await context.GetRequiredService<ICurrencyService>().GetAllCurrenciesAsync();
        context.SetCurrencies(currencies, GetCultureName(request));
    }

    protected override Task AfterMediatorSend(IResolveFieldContext<object> context, TQuery request, TResult response)
    {
        foreach (var aggregate in GetOrderAggregates(response))
        {
            context.SetExpandedObjectGraph(aggregate);
        }

        return Task.CompletedTask;
    }
}
