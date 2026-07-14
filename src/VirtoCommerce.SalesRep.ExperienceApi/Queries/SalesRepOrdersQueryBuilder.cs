using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrdersQueryBuilder : SearchQueryBuilder<SalesRepOrdersQuery, SalesRepOrderSearchResult, SalesRepOrder, SalesRepOrderType>
{
    protected override string Name => "salesRepOrders";

    public SalesRepOrdersQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepOrdersQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        // Propagate this field's arguments (notably cultureName) to the UserContext so the per-item
        // SalesRepOrderType.statusDisplayValue LocalizedField resolver can read the culture (the X-Order pattern).
        context.CopyArgumentsToUserContext();
    }
}
