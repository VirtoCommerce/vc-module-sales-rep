using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrdersQueryBuilder : SalesRepSearchQueryBuilder<SalesRepOrdersQuery, SalesRepOrderSearchResult, SalesRepOrder, SalesRepOrderType>
{
    protected override string Name => "salesRepOrders";

    public SalesRepOrdersQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
