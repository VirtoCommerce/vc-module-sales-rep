using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartKindsQueryHandler : IQueryHandler<SalesRepCartKindsQuery, IList<SalesRepCartKind>>
{
    private readonly ISalesRepCartKindService _kindService;

    public SalesRepCartKindsQueryHandler(ISalesRepCartKindService kindService)
    {
        _kindService = kindService;
    }

    public virtual Task<IList<SalesRepCartKind>> Handle(SalesRepCartKindsQuery request, CancellationToken cancellationToken)
        => _kindService.GetRulesAsync(request.StoreId, request.CultureName);
}
