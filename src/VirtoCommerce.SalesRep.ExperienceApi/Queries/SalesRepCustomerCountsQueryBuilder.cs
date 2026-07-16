using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerCountsQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerCountsQuery, SalesRepCustomerCountsContext, SalesRepCustomerCountsType>
{
    protected override string Name => "salesRepCustomerCounts";

    public SalesRepCustomerCountsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
