using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartFilterRulesQueryHandler : IQueryHandler<SalesRepCartFilterRulesQuery, IList<SalesRepCartFilterRule>>
{
    private readonly ISalesRepCartFilterRuleResolver _kindService;

    public SalesRepCartFilterRulesQueryHandler(ISalesRepCartFilterRuleResolver kindService)
    {
        _kindService = kindService;
    }

    public virtual Task<IList<SalesRepCartFilterRule>> Handle(SalesRepCartFilterRulesQuery request, CancellationToken cancellationToken)
        => _kindService.GetRulesAsync(request.StoreId, request.CultureName);
}
