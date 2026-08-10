using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepLayoutQueryHandler(ILayoutService layoutService)
    : IQueryHandler<SalesRepLayoutQuery, Layout>
{
    public virtual Task<Layout> Handle(SalesRepLayoutQuery request, CancellationToken cancellationToken)
    {
        return layoutService.GetLayoutAsync(request.UserId, request.Scope, request.StoreId);
    }
}
