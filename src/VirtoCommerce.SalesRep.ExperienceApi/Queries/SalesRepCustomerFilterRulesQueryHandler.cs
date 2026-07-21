using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerFilterRulesQueryHandler : IQueryHandler<SalesRepCustomerFilterRulesQuery, IList<SalesRepCustomerFilterRule>>
{
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;

    public SalesRepCustomerFilterRulesQueryHandler(ISalesRepCustomerFilterRuleResolver filterRuleResolver)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    public virtual Task<IList<SalesRepCustomerFilterRule>> Handle(SalesRepCustomerFilterRulesQuery request, CancellationToken cancellationToken)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
