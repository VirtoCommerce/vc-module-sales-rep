using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepOrderFilterRulesQuery, IList<SalesRepOrderFilterRule>, ListGraphType<SalesRepOrderFilterRuleType>>
{
    protected override string Name => "salesRepOrderFilterRules";

    public SalesRepOrderFilterRulesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
