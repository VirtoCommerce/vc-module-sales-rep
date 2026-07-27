using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepTopSellerSortRulesQuery, SalesRepTopSellerSortRule>
{
    private readonly ISalesRepTopSellerSortRuleResolver _sortRuleResolver;

    public SalesRepTopSellerSortRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepTopSellerSortRuleResolver sortRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepTopSellerSortRule>> GetRulesAsync(SalesRepTopSellerSortRulesQuery request)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
