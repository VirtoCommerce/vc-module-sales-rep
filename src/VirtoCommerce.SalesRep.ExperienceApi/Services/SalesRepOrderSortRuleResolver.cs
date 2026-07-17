using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default orders-list ordering source: a single "recent" rule (created date, newest first). Both the default and
/// any unrecognized selection resolve to the first rule, so the list is always deterministically ordered. Projects
/// override this service to add orderings (e.g. "biggest by total" → "total:desc").
/// </summary>
public class SalesRepOrderSortRuleResolver : ISalesRepOrderSortRuleResolver
{
    /// <summary>Name of the built-in "recent" ordering — created date, newest first (the default).</summary>
    public const string RecentRuleName = "recent";

    public virtual Task<IList<SalesRepOrderSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepOrderSortRule>>(
        [
            SalesRepOrderSortRule.Create(RecentRuleName, "Most recent", "createdDate:desc"),
        ]);

    public virtual async Task<CustomerOrderSearchCriteria> ApplySortAsync(string storeId, string sort, CustomerOrderSearchCriteria criteria)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        var rule = string.IsNullOrEmpty(sort)
            ? null
            : rules.FirstOrDefault(x => string.Equals(x.Name, sort, StringComparison.OrdinalIgnoreCase));

        // Default (and fallback for an unrecognized name): the first configured rule.
        rule ??= rules.FirstOrDefault();

        if (!string.IsNullOrEmpty(rule?.Sort))
        {
            criteria.Sort = rule.Sort;
        }

        return criteria;
    }
}
