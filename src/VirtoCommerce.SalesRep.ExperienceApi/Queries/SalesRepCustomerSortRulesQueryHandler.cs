using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepCustomerSortRulesQuery, SalesRepCustomerSortRule>
{
    private readonly ISalesRepCustomerSortRuleResolver _sortRuleResolver;

    public SalesRepCustomerSortRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerSortRuleResolver sortRuleResolver)
        : base(organizationAccessService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepCustomerSortRule>> GetRulesAsync(SalesRepCustomerSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
