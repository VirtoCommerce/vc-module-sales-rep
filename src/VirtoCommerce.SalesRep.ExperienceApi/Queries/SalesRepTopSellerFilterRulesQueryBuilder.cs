using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQueryBuilder : SalesRepQueryBuilder<SalesRepTopSellerFilterRulesQuery, IList<SalesRepTopSellerFilterRule>, ListGraphType<SalesRepTopSellerFilterRuleType>>
{
    protected override string Name => "salesRepTopSellerFilterRules";

    public SalesRepTopSellerFilterRulesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
