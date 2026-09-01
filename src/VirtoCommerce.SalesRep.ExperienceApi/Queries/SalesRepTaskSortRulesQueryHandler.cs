using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskSortRulesQueryHandler : SalesRepTaskRulesQueryHandlerBase<SalesRepTaskSortRulesQuery, SalesRepTaskSortRule>
{
    private readonly ISalesRepTaskSortRuleResolver _sortRuleResolver;

    public SalesRepTaskSortRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepTaskSortRuleResolver sortRuleResolver)
        : base(organizationAccessService)
    {
        _sortRuleResolver = sortRuleResolver;
    }

    protected override Task<IList<SalesRepTaskSortRule>> GetRulesAsync(SalesRepTaskSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
