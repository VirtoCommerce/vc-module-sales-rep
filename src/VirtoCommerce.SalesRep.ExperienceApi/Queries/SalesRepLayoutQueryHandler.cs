using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepLayoutQueryHandler(ILayoutService layoutService)
    : IQueryHandler<SalesRepLayoutQuery, Layout>
{
    // Returns null when the rep has never saved this surface; the storefront then renders its registry default.
    public virtual Task<Layout> Handle(SalesRepLayoutQuery request, CancellationToken cancellationToken)
    {
        return layoutService.GetLayoutAsync(request.UserId, request.Scope, request.StoreId);
    }
}
