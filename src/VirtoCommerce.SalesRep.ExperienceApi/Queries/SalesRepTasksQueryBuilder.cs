using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTasksQueryBuilder : SalesRepSearchQueryBuilder<SalesRepTasksQuery, SalesRepTaskSearchResult, SalesRepTask, SalesRepTaskType>
{
    protected override string Name => "salesRepTasks";

    public SalesRepTasksQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
