using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepTopSellerFilterRulesQuery, IList<SalesRepTopSellerFilterRule>, ListGraphType<SalesRepTopSellerFilterRuleType>>
{
    protected override string Name => "salesRepTopSellerFilterRules";

    public SalesRepTopSellerFilterRulesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
