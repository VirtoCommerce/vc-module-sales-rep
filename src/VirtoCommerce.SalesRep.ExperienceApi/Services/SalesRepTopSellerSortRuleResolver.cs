using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepTopSellerSortRuleResolver : SortRuleResolverBase<SalesRepTopSellerSortRule>, ISalesRepTopSellerSortRuleResolver
{
    public const string ByUnitsRuleName = "by-units";

    public const string ByRevenueRuleName = "by-revenue";

    public override Task<IList<SalesRepTopSellerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepTopSellerSortRule>>(
        [
            SalesRepTopSellerSortRule.Create(ByUnitsRuleName, "By units sold", SalesRepTopSellerSortBy.Units, SortDirection.Descending, supportsDirection: false),
            SalesRepTopSellerSortRule.Create(ByRevenueRuleName, "By revenue", SalesRepTopSellerSortBy.Revenue, SortDirection.Descending, supportsDirection: false),
        ]);

    public virtual async Task<SalesRepTopSellerCriteria> ApplySortAsync(string storeId, string sort, SalesRepTopSellerCriteria criteria)
    {
        var (rule, _) = await ResolveSortRuleAsync(storeId, sort);

        if (rule != null)
        {
            criteria.SortBy = rule.SortBy;
        }

        return criteria;
    }
}
