using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
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
    protected SalesRepQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureAuthenticated();
    }
}
