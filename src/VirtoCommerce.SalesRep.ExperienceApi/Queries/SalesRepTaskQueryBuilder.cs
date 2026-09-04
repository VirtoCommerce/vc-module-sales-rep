using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskQueryBuilder : SalesRepQueryBuilder<SalesRepTaskQuery, SalesRepTask, SalesRepTaskType>
{
    protected override string Name => "salesRepTask";

    public SalesRepTaskQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
