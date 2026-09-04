using System.Collections.Generic;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskTypesQueryBuilder : SalesRepQueryBuilder<SalesRepTaskTypesQuery, IList<string>, ListGraphType<StringGraphType>>
{
    protected override string Name => "salesRepTaskTypes";

    public SalesRepTaskTypesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
