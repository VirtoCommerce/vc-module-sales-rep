using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepTaskFilterRulesQuery, IList<SalesRepTaskFilterRule>, ListGraphType<SalesRepTaskFilterRuleType>>
{
    protected override string Name => "salesRepTaskFilterRules";

    public SalesRepTaskFilterRulesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
