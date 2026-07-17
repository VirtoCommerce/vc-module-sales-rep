using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default orders-list ordering source: a single "recent" rule (created date, newest first). Both the default and
/// any unrecognized selection resolve to the first rule, so the list is always deterministically ordered. Projects
/// override this service to add orderings (e.g. "biggest by total" → "total:desc").
/// </summary>
public class SalesRepOrderSortRuleResolver : SortRuleResolverBase<SalesRepOrderSortRule>, ISalesRepOrderSortRuleResolver
{
    /// <summary>Name of the built-in "recent" ordering — created date, newest first (the default).</summary>
    public const string RecentRuleName = "recent";

    public override Task<IList<SalesRepOrderSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepOrderSortRule>>(
        [
            SalesRepOrderSortRule.Create(RecentRuleName, "Most recent", "createdDate:desc"),
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
