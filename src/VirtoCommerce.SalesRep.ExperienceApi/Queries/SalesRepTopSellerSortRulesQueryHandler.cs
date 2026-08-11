using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepTopSellerSortRulesQuery, SalesRepTopSellerSortRule>
{
    private readonly ISalesRepTopSellerSortRuleResolver _sortRuleResolver;

    public SalesRepTopSellerSortRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTopSellerSortRuleResolver sortRuleResolver)
        : base(organizationAccessService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepTopSellerSortRule>> GetRulesAsync(SalesRepTopSellerSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
