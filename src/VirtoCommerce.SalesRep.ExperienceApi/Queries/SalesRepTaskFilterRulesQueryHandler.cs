using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskFilterRulesQueryHandler : SalesRepTaskRulesQueryHandlerBase<SalesRepTaskFilterRulesQuery, SalesRepTaskFilterRule>
{
    private readonly ISalesRepTaskFilterRuleResolver _filterRuleResolver;

    public SalesRepTaskFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTaskFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override Task<IList<SalesRepTaskFilterRule>> GetRulesAsync(SalesRepTaskFilterRulesQuery request, IList<string> organizationIds)
    {
        var context = SalesRepFilterRuleContext.Create(
            request.StoreId, request.CultureName, organizationIds: null, customerId: null);

        return _filterRuleResolver.GetRulesAsync(context);
    }
}
