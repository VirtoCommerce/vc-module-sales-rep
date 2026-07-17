using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerOrderStatisticsQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext, CustomerOrderStatisticsType>
{
    protected override string Name => "salesRepCustomerOrderStatistics";

    public SalesRepCustomerOrderStatisticsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepCustomerOrderStatisticsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        // Propagate this field's arguments (notably cultureName) to the UserContext so the nested MoneyType
        // resolvers on period/comparison can read the culture for their formatted amounts.
        context.CopyArgumentsToUserContext();
    }
}
