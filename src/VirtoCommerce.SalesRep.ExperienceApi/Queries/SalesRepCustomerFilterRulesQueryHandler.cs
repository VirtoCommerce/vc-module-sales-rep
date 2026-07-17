using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerFilterRulesQueryHandler : IQueryHandler<SalesRepCustomerFilterRulesQuery, IList<SalesRepCustomerFilterRule>>
{
    private readonly ISalesRepCustomerFilterRuleResolver _resolver;

    public SalesRepCustomerFilterRulesQueryHandler(ISalesRepCustomerFilterRuleResolver resolver)
    {
        _resolver = resolver;
    }

    public virtual Task<IList<SalesRepCustomerFilterRule>> Handle(SalesRepCustomerFilterRulesQuery request, CancellationToken cancellationToken)
        => _resolver.GetRulesAsync(request.StoreId, request.CultureName);
}
