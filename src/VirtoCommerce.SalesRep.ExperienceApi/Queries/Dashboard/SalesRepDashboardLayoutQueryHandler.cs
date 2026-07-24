using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Dashboard;

public class SalesRepDashboardLayoutQueryHandler(IDashboardLayoutService dashboardLayoutService)
    : IQueryHandler<SalesRepDashboardLayoutQuery, DashboardLayout>
{
    // Returns null when the rep has never saved this surface; the storefront then renders its registry default.
    public virtual Task<DashboardLayout> Handle(SalesRepDashboardLayoutQuery request, CancellationToken cancellationToken)
    {
        return dashboardLayoutService.GetLayoutAsync(request.UserId, request.Scope, request.StoreId);
    }
}
