using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepTopSellerFilterRulesQuery, SalesRepTopSellerFilterRule>
{
    private readonly ISalesRepTopSellerFilterRuleResolver _filterRuleResolver;

    public SalesRepTopSellerFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTopSellerFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepTopSellerFilterRule> FilterRuleResolver => _filterRuleResolver;
}
