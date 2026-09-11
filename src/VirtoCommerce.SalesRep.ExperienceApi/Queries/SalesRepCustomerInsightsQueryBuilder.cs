using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerInsightsQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerInsightsQuery, SalesRepCustomerInsightsContext, SalesRepCustomerInsightsType>
{
    protected override string Name => "salesRepCustomerInsights";

    public SalesRepCustomerInsightsQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerInsightsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();
    }
}
