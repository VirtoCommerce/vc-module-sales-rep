using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerSortRulesQueryHandler : IQueryHandler<SalesRepCustomerSortRulesQuery, IList<SalesRepCustomerSortRule>>
{
    private readonly ISalesRepCustomerSortRuleResolver _sortRuleResolver;

    public SalesRepCustomerSortRulesQueryHandler(ISalesRepCustomerSortRuleResolver sortRuleResolver)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    public virtual Task<IList<SalesRepCustomerSortRule>> Handle(SalesRepCustomerSortRulesQuery request, CancellationToken cancellationToken)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
