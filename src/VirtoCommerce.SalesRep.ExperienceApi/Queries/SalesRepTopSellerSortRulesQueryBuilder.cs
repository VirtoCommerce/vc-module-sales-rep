using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerSortRulesQueryBuilder : SalesRepQueryBuilder<SalesRepTopSellerSortRulesQuery, IList<SalesRepTopSellerSortRule>, ListGraphType<SalesRepTopSellerSortRuleType>>
{
    protected override string Name => "salesRepTopSellerSortRules";

    public SalesRepTopSellerSortRulesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
