using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderStatusesQueryHandler : IQueryHandler<SalesRepOrderStatusesQuery, IList<SalesRepOrderStatus>>
{
    private readonly ISalesRepOrderStatusService _statusService;

    public SalesRepOrderStatusesQueryHandler(ISalesRepOrderStatusService statusService)
    {
        _statusService = statusService;
    }

    public virtual Task<IList<SalesRepOrderStatus>> Handle(SalesRepOrderStatusesQuery request, CancellationToken cancellationToken)
        => _statusService.GetStatusesAsync(request.StoreId, request.CultureName);
}
