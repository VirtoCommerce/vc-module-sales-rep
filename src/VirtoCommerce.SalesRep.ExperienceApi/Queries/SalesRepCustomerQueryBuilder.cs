using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerQuery, SalesRepCustomerDetails, SalesRepCustomerDetailsType>
{
    protected override string Name => "salesRepCustomer";

    public SalesRepCustomerQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
