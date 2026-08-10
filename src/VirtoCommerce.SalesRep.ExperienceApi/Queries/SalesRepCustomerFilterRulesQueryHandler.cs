using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepCustomerFilterRulesQuery, SalesRepCustomerFilterRule>
{
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;

    public SalesRepCustomerFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepCustomerFilterRule> FilterRuleResolver => _filterRuleResolver;
}
