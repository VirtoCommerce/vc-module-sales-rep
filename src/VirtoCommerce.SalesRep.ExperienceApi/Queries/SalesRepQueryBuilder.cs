using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using System;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepQueryBuilder<TQuery, TResult, TResultGraphType>
    : QueryBuilder<TQuery, TResult, TResultGraphType>
    where TQuery : IQuery<TResult>, IExtendableQuery, IHasArguments
    where TResultGraphType : IGraphType
{
    protected SalesRepQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    protected SalesRepQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();
    }
}
