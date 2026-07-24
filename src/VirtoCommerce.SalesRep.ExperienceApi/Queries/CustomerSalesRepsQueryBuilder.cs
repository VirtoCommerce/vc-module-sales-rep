using System;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class CustomerSalesRepsQueryBuilder : SalesRepSearchQueryBuilder<CustomerSalesRepsQuery, SalesRepContactSearchResult, SalesRepContact, SalesRepContactType>
{
    protected override string Name => "customerSalesReps";

    public CustomerSalesRepsQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    [Obsolete("Use the constructor without IMediator. The mediator is resolved from context.RequestServices per request.", DiagnosticId = "VC0015", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
    public CustomerSalesRepsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : this(authorizationService)
    {
    }
}
