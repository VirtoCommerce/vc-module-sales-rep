using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepCartFilterRulesQuery, IList<SalesRepCartFilterRule>, ListGraphType<SalesRepCartFilterRuleType>>
{
    protected override string Name => "salesRepCartFilterRules";

    public SalesRepCartFilterRulesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
