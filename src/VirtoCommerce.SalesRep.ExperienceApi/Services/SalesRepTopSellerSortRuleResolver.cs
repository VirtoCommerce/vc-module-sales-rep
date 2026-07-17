using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "Top Sellers" ordering source: "by-units" (sum of quantities, the default) and "by-revenue" (sum of
/// quantity × price). Both the default and any unrecognized selection resolve to the first rule. Projects override
/// this service to add orderings.
/// </summary>
public class SalesRepTopSellerSortRuleResolver : ISalesRepTopSellerSortRuleResolver
{
    /// <summary>Rank by total units sold (the default).</summary>
    public const string ByUnitsRuleName = "by-units";

    /// <summary>Rank by total revenue (quantity × price).</summary>
    public const string ByRevenueRuleName = "by-revenue";

    public virtual Task<IList<SalesRepTopSellerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepTopSellerSortRule>>(
        [
            SalesRepTopSellerSortRule.Create(ByUnitsRuleName, "By units sold", SalesRepTopSellerSortBy.Units),
            SalesRepTopSellerSortRule.Create(ByRevenueRuleName, "By revenue", SalesRepTopSellerSortBy.Revenue),
        ]);

    public virtual async Task<SalesRepTopSellerCriteria> ApplySortAsync(string storeId, string sort, SalesRepTopSellerCriteria criteria)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        var rule = string.IsNullOrEmpty(sort)
            ? null
            : rules.FirstOrDefault(x => string.Equals(x.Name, sort, StringComparison.OrdinalIgnoreCase));

        // Default (and fallback for an unrecognized name): the first configured rule.
        rule ??= rules.FirstOrDefault();

        if (rule != null)
        {
            criteria.SortBy = rule.SortBy;
        }

        return criteria;
    }
}
