using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartFilterRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepCartFilterRulesQuery, SalesRepCartFilterRule>
{
    private readonly ISalesRepCartFilterRuleResolver _filterRuleResolver;

    public SalesRepCartFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCartFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override Task<IList<SalesRepCartFilterRule>> GetRulesAsync(SalesRepCartFilterRulesQuery request)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
