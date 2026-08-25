using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerActivitySummaryQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerActivitySummaryQuery, SalesRepCustomerActivitySummary, SalesRepCustomerActivitySummaryType>
{
    protected override string Name => "salesRepCustomerActivitySummary";

    public SalesRepCustomerActivitySummaryQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerActivitySummaryQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();
    }
}
