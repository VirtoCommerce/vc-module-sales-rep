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

/// <summary>
/// Base builder for the Sales Rep single-result queries. Enforces the module-wide access rule in one place — the
/// caller must be an authenticated Sales Rep — so a new query can't ship without the guard. Derived builders that
/// need extra pre-send work (e.g. propagating arguments) override <see cref="BeforeMediatorSend"/> and call
/// <c>base.BeforeMediatorSend</c> first.
/// </summary>
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
