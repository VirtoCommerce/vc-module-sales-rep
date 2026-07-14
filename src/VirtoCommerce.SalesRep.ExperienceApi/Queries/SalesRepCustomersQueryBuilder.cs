using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQueryBuilder : SalesRepSearchQueryBuilder<SalesRepCustomersQuery, SalesRepCustomerSearchResult, SalesRepCustomer, SalesRepCustomerType>
{
    protected override string Name => "salesRepCustomers";

    public SalesRepCustomersQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
