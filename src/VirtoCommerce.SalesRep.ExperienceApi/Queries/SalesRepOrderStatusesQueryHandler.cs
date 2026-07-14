using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderStatusesQueryHandler : IQueryHandler<SalesRepOrderStatusesQuery, SalesRepOrderStatusesResult>
{
    private readonly ISalesRepOrderStatusService _statusService;

    public SalesRepOrderStatusesQueryHandler(ISalesRepOrderStatusService statusService)
    {
        _statusService = statusService;
    }

    public virtual async Task<SalesRepOrderStatusesResult> Handle(SalesRepOrderStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepOrderStatusesResult>.TryCreateInstance();
        result.Items = await _statusService.GetStatusesAsync(request.StoreId, request.CultureName);
        return result;
    }
}
