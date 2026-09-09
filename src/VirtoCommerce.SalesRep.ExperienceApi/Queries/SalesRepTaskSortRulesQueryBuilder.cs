using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskSortRulesQueryBuilder : SalesRepQueryBuilder<SalesRepTaskSortRulesQuery, IList<SalesRepTaskSortRule>, ListGraphType<SalesRepTaskSortRuleType>>
{
    protected override string Name => "salesRepTaskSortRules";

    public SalesRepTaskSortRulesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
