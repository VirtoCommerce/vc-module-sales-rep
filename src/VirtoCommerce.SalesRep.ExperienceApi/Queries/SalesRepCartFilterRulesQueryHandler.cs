using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCartFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepCartFilterRulesQuery, SalesRepCartFilterRule>
{
    private readonly ISalesRepCartFilterRuleResolver _filterRuleResolver;

    public SalesRepCartFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCartFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepCartFilterRule> FilterRuleResolver => _filterRuleResolver;
}
