using System;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepSearchQueryBuilder<TQuery, TResult, TItem, TItemGraphType>
    : SearchQueryBuilder<TQuery, TResult, TItem, TItemGraphType>
    where TQuery : IQuery<TResult>, IExtendableQuery, IHasArguments, ISearchQuery
    where TResult : GenericSearchResult<TItem>
    where TItemGraphType : IGraphType
{
    protected SalesRepSearchQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    protected SalesRepSearchQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, TQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        await context.EnsureAuthenticatedAsync();

        context.CopyArgumentsToUserContext();
    }
}
