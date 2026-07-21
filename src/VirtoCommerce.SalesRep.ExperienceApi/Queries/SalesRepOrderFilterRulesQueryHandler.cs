using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryHandler : IQueryHandler<SalesRepOrderFilterRulesQuery, IList<SalesRepOrderFilterRule>>
{
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public SalesRepOrderFilterRulesQueryHandler(ISalesRepOrderFilterRuleResolver filterRuleResolver)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    public virtual Task<IList<SalesRepOrderFilterRule>> Handle(SalesRepOrderFilterRulesQuery request, CancellationToken cancellationToken)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
