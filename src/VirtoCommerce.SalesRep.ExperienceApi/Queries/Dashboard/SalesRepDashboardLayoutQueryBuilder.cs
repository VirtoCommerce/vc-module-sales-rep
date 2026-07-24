using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Dashboard;

public class SalesRepDashboardLayoutQueryBuilder : SalesRepQueryBuilder<SalesRepDashboardLayoutQuery, DashboardLayout, SalesRepDashboardLayoutType>
{
    protected override string Name => "salesRepDashboardLayout";

    public SalesRepDashboardLayoutQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }
}
