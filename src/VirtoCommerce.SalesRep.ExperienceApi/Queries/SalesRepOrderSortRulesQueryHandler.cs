using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepOrderSortRulesQuery, SalesRepOrderSortRule>
{
    private readonly ISalesRepOrderSortRuleResolver _sortRuleResolver;

    public SalesRepOrderSortRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepOrderSortRuleResolver sortRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepOrderSortRule>> GetRulesAsync(SalesRepOrderSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
