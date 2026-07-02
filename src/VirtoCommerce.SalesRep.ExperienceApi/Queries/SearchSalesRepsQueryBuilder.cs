using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.CustomerModule.Core.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SearchSalesRepsQueryBuilder : SearchQueryBuilder<SearchSalesRepsQuery, SalesRepContactSearchResult, SalesRepContact, SalesRepContactType>
{
    protected override string Name => "mySalesReps";

    public SearchSalesRepsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SearchSalesRepsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        // Scope to the caller's own organization; the handler returns nothing when there is no org context.
        request.OrganizationId = context.GetCurrentPrincipal()?.GetCurrentOrganizationId();
    }
}
