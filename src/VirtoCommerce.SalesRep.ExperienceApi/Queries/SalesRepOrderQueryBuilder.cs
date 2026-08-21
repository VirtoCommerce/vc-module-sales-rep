using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Shared plumbing for the queries answering with X-Order's CustomerOrderType: its money fields read the
/// currencies off the user context, and every returned aggregate has to be registered for field expansion.
/// </summary>
public abstract class SalesRepOrderQueryBuilder<TQuery, TResult> : SalesRepQueryBuilder<TQuery, TResult, CustomerOrderType>
    where TQuery : IQuery<TResult>, IExtendableQuery, IHasArguments
{
    protected SalesRepOrderQueryBuilder(IAuthorizationService authorizationService, ICurrencyService currencyService)
        : base(authorizationService)
    {
        CurrencyService = currencyService;
    }

    protected ICurrencyService CurrencyService { get; }

    protected abstract string GetCultureName(TQuery request);

    protected abstract IEnumerable<CustomerOrderAggregate> GetOrderAggregates(TResult response);

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        // CustomerOrderType's localized fields (statusDisplayValue, dynamic properties) declare no cultureName
        // argument of their own — they read it from the user context, so the arguments have to be copied there.
        context.CopyArgumentsToUserContext();

        var currencies = await CurrencyService.GetAllCurrenciesAsync();
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
