using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerFilterRulesQuery, IList<SalesRepCustomerFilterRule>, ListGraphType<SalesRepCustomerFilterRuleType>>
{
    protected override string Name => "salesRepCustomerFilterRules";

    public SalesRepCustomerFilterRulesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
