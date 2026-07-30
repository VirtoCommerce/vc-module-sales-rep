using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepCartFilterRulesQuery, SalesRepCartFilterRule>
{
    private readonly ISalesRepCartFilterRuleResolver _filterRuleResolver;

    public SalesRepCartFilterRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCartFilterRuleResolver filterRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepCartFilterRule> FilterRuleResolver => _filterRuleResolver;
}
