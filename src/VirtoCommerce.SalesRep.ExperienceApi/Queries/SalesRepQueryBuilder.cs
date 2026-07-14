using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Security.Authorization;

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
    protected SalesRepQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }
    }
}
