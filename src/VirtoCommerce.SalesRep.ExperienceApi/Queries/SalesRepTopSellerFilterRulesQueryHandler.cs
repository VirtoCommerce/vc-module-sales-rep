using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQueryHandler : IQueryHandler<SalesRepTopSellerFilterRulesQuery, IList<SalesRepTopSellerFilterRule>>
{
    private readonly ISalesRepTopSellerFilterRuleResolver _filterRuleResolver;

    public SalesRepTopSellerFilterRulesQueryHandler(ISalesRepTopSellerFilterRuleResolver filterRuleResolver)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    public virtual Task<IList<SalesRepTopSellerFilterRule>> Handle(SalesRepTopSellerFilterRulesQuery request, CancellationToken cancellationToken)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
