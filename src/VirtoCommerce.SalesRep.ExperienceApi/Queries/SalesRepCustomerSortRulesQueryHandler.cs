using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepCustomerSortRulesQuery, SalesRepCustomerSortRule>
{
    private readonly ISalesRepCustomerSortRuleResolver _sortRuleResolver;

    public SalesRepCustomerSortRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCustomerSortRuleResolver sortRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepCustomerSortRule>> GetRulesAsync(SalesRepCustomerSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
