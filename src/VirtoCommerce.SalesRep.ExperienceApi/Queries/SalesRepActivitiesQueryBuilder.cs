using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepActivitiesQueryBuilder : SalesRepQueryBuilder<SalesRepActivitiesQuery, SalesRepActivitySearchResult, SalesRepActivityConnectionType>
{
    protected override string Name => "salesRepActivities";

    public SalesRepActivitiesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepActivitiesQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();
    }
}
