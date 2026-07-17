using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderSortRulesQueryHandler : IQueryHandler<SalesRepOrderSortRulesQuery, IList<SalesRepOrderSortRule>>
{
    private readonly ISalesRepOrderSortRuleResolver _sortRuleResolver;

    public SalesRepOrderSortRulesQueryHandler(ISalesRepOrderSortRuleResolver sortRuleResolver)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    public virtual Task<IList<SalesRepOrderSortRule>> Handle(SalesRepOrderSortRulesQuery request, CancellationToken cancellationToken)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
