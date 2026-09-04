using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepTaskFilterRulesQuery, SalesRepTaskFilterRule>
{
    public SalesRepTaskFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTaskFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        FilterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepTaskFilterRule> FilterRuleResolver { get; }
}
