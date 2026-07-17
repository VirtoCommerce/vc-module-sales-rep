using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerSortRulesQueryHandler : IQueryHandler<SalesRepTopSellerSortRulesQuery, IList<SalesRepTopSellerSortRule>>
{
    private readonly ISalesRepTopSellerSortRuleResolver _sortRuleResolver;

    public SalesRepTopSellerSortRulesQueryHandler(ISalesRepTopSellerSortRuleResolver sortRuleResolver)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    public virtual Task<IList<SalesRepTopSellerSortRule>> Handle(SalesRepTopSellerSortRulesQuery request, CancellationToken cancellationToken)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
