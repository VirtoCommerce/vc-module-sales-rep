using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderSortRulesQueryBuilder : SalesRepQueryBuilder<SalesRepOrderSortRulesQuery, IList<SalesRepOrderSortRule>, ListGraphType<SalesRepOrderSortRuleType>>
{
    protected override string Name => "salesRepOrderSortRules";

    public SalesRepOrderSortRulesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
