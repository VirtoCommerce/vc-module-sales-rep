using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellersQueryBuilder : SalesRepQueryBuilder<SalesRepTopSellersQuery, IList<SalesRepTopSeller>, ListGraphType<SalesRepTopSellerType>>
{
    protected override string Name => "salesRepTopSellers";

    public SalesRepTopSellersQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepTopSellersQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        // Propagate this field's arguments (notably cultureName) to the UserContext so the nested
        // SalesRepTopSellerType.revenue MoneyType resolver can read the culture for its formatted amount.
        context.CopyArgumentsToUserContext();
    }
}
