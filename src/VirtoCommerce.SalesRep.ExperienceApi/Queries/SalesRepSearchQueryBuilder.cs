using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Base builder for the Sales Rep search (paged connection) queries. Enforces the module-wide rules in one place —
/// the caller must be an authenticated Sales Rep, and this field's arguments (e.g. cultureName) are propagated to the
/// UserContext for nested item resolvers — so a new search query can't ship without either. Derived builders that
/// need further pre-send work override <see cref="BeforeMediatorSend"/> and call <c>base.BeforeMediatorSend</c> first.
/// </summary>
public abstract class SalesRepSearchQueryBuilder<TQuery, TResult, TItem, TItemGraphType>
    : SearchQueryBuilder<TQuery, TResult, TItem, TItemGraphType>
    where TQuery : IQuery<TResult>, IExtendableQuery, IHasArguments, ISearchQuery
    where TResult : GenericSearchResult<TItem>
    where TItemGraphType : IGraphType
{
    protected SalesRepSearchQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
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

        // Propagate this field's arguments (notably cultureName) to the UserContext so nested item resolvers can read
        // them — e.g. SalesRepOrderType.statusDisplayValue / total.formattedAmount on a customer's lastOrder. A guarded
        // no-op when there are no arguments; centralizing it here (as X-Order's BaseSearchOrderQueryBuilder does) means
        // a new search query can't ship without culture propagation.
        context.CopyArgumentsToUserContext();
    }
}
