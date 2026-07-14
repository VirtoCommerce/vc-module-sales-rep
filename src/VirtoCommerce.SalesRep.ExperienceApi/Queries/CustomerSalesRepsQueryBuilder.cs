using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class CustomerSalesRepsQueryBuilder : SalesRepSearchQueryBuilder<CustomerSalesRepsQuery, SalesRepContactSearchResult, SalesRepContact, SalesRepContactType>
{
    protected override string Name => "customerSalesReps";

    public CustomerSalesRepsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
