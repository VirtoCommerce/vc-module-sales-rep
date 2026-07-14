using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderStatusesQueryBuilder : SalesRepQueryBuilder<SalesRepOrderStatusesQuery, IList<SalesRepOrderStatus>, ListGraphType<SalesRepOrderStatusType>>
{
    protected override string Name => "salesRepOrderStatuses";

    public SalesRepOrderStatusesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
