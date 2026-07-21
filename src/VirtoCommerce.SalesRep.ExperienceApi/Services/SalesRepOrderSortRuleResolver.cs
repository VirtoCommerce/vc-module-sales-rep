using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default orders-list ordering source: "recent" (created date, newest first — the default, one-way) and "total"
/// (by order value; biggest first by default, "total:asc" for smallest first). Both the default and any unrecognized
/// selection resolve to the first rule, so the list is always deterministically ordered. Projects override this
/// service to add or replace orderings.
/// </summary>
public class SalesRepOrderSortRuleResolver : SortRuleResolverBase<SalesRepOrderSortRule>, ISalesRepOrderSortRuleResolver
{
    /// <summary>Name of the built-in "recent" ordering — created date, newest first (the default; one-way).</summary>
    public const string RecentRuleName = "recent";

    /// <summary>Name of the built-in "total" ordering — by order value; biggest first by default, "total:asc" for smallest first.</summary>
    public const string TotalRuleName = "total";

    public override Task<IList<SalesRepOrderSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepOrderSortRule>>(
        [
            SalesRepOrderSortRule.Create(RecentRuleName, "Most recent", "createdDate", SortDirection.Descending, supportsDirection: false),
            SalesRepOrderSortRule.Create(TotalRuleName, "Order total", "total", SortDirection.Descending, supportsDirection: true),
        ]);

    public virtual async Task<CustomerOrderSearchCriteria> ApplySortAsync(string storeId, string sort, CustomerOrderSearchCriteria criteria)
    {
        var (rule, direction) = await ResolveSortRuleAsync(storeId, sort);

        if (!string.IsNullOrEmpty(rule?.SortField))
        {
            criteria.Sort = $"{rule.SortField}:{direction.ToToken()}";
        }

        return criteria;
    }
}
