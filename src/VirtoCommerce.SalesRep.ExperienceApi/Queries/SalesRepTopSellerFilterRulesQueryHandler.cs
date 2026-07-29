using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepTopSellerFilterRulesQuery, SalesRepTopSellerFilterRule>
{
    private readonly ISalesRepTopSellerFilterRuleResolver _filterRuleResolver;

    public SalesRepTopSellerFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTopSellerFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override Task<IList<SalesRepTopSellerFilterRule>> GetRulesAsync(SalesRepTopSellerFilterRulesQuery request)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
