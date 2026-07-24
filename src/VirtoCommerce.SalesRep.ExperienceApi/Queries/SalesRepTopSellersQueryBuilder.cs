using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellersQueryBuilder : SalesRepQueryBuilder<SalesRepTopSellersQuery, IList<SalesRepTopSeller>, ListGraphType<SalesRepTopSellerType>>
{
    protected override string Name => "salesRepTopSellers";

    public SalesRepTopSellersQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepTopSellersQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.CopyArgumentsToUserContext();
    }
}
