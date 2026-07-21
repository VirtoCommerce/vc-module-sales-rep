using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default orders-list ordering source: "recent" (created date, newest first — the default) and "largest/smallest
/// total" (by order total). Both the default and any unrecognized selection resolve to the first rule, so the list is
/// always deterministically ordered. Projects override this service to add or replace orderings.
/// </summary>
public class SalesRepOrderSortRuleResolver : SortRuleResolverBase<SalesRepOrderSortRule>, ISalesRepOrderSortRuleResolver
{
    /// <summary>Name of the built-in "recent" ordering — created date, newest first (the default).</summary>
    public const string RecentRuleName = "recent";

    /// <summary>Largest order total first.</summary>
    public const string LargestTotalRuleName = "largest-total";

    /// <summary>Smallest order total first.</summary>
    public const string SmallestTotalRuleName = "smallest-total";

    public override Task<IList<SalesRepOrderSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepOrderSortRule>>(
        [
            SalesRepOrderSortRule.Create(RecentRuleName, "Most recent", "createdDate:desc"),
            SalesRepOrderSortRule.Create(LargestTotalRuleName, "Largest total", "total:desc"),
            SalesRepOrderSortRule.Create(SmallestTotalRuleName, "Smallest total", "total:asc"),
        ]);

    public virtual async Task<CustomerOrderSearchCriteria> ApplySortAsync(string storeId, string sort, CustomerOrderSearchCriteria criteria)
    {
        var rule = await ResolveRuleAsync(storeId, sort);

        if (!string.IsNullOrEmpty(rule?.Sort))
        {
            criteria.Sort = rule.Sort;
        }

        return criteria;
    }
}
