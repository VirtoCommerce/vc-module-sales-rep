using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepOrderFilterRulesQuery, SalesRepOrderFilterRule>
{
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public SalesRepOrderFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepOrderFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepOrderFilterRule> FilterRuleResolver => _filterRuleResolver;
}
