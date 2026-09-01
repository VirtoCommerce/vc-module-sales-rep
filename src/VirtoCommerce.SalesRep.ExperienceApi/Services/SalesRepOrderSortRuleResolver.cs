using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderSortRuleResolver : SortRuleResolverBase<SalesRepOrderSortRule>, ISalesRepOrderSortRuleResolver
{
    public const string RecentRuleName = "recent";

    public const string TotalRuleName = "total";

    public override Task<IList<SalesRepOrderSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepOrderSortRule>>(
        [
            SalesRepOrderSortRule.Create(RecentRuleName, "Most recent", "createdDate", SortDirection.Descending, supportsDirection: false),
            SalesRepOrderSortRule.Create(TotalRuleName, "Order total", "total", SortDirection.Descending, supportsDirection: true),
        ]);

    public virtual async Task<CustomerOrderSearchCriteria> ApplySortAsync(string storeId, string sort, CustomerOrderSearchCriteria criteria)
    {
        await ApplyResolvedSortAsync(storeId, sort, criteria);

        return criteria;
    }
}
