using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepOrderFilterRulesQuery, SalesRepOrderFilterRule>
{
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public SalesRepOrderFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepOrderFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(SalesRepOrderFilterRulesQuery request)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
