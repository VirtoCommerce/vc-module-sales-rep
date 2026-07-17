using System.Collections.Generic;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerSortRulesQueryBuilder : SalesRepQueryBuilder<SalesRepCustomerSortRulesQuery, IList<SalesRepCustomerSortRule>, ListGraphType<SalesRepCustomerSortRuleType>>
{
    protected override string Name => "salesRepCustomerSortRules";

    public SalesRepCustomerSortRulesQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
        : base(mediator, authorizationService)
    {
    }
}
