using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrderStatisticsQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext, CustomerOrderStatisticsType>
{
    protected override string Name => "salesRepCustomerOrderStatistics";

    public SalesRepCustomerOrderStatisticsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
