using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepOrderFilterRulesQuery, SalesRepOrderFilterRule>
{
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public SalesRepOrderFilterRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepOrderFilterRuleResolver filterRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepOrderFilterRule> FilterRuleResolver => _filterRuleResolver;
}
