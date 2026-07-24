using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepLayoutQueryBuilder : SalesRepQueryBuilder<SalesRepLayoutQuery, Layout, SalesRepLayoutType>
{
    protected override string Name => "salesRepLayout";

    public SalesRepLayoutQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
