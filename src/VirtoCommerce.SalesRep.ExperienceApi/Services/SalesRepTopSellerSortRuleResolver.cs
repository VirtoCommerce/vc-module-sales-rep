using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "Top Sellers" ordering source: "by-units" (sum of quantities, the default) and "by-revenue" (sum of
/// quantity × price). Both the default and any unrecognized selection resolve to the first rule. Projects override
/// this service to add orderings.
/// </summary>
public class SalesRepTopSellerSortRuleResolver : SortRuleResolverBase<SalesRepTopSellerSortRule>, ISalesRepTopSellerSortRuleResolver
{
    /// <summary>Rank by total units sold (the default).</summary>
    public const string ByUnitsRuleName = "by-units";

    /// <summary>Rank by total revenue (quantity × price).</summary>
    public const string ByRevenueRuleName = "by-revenue";

    public override Task<IList<SalesRepTopSellerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepTopSellerSortRule>>(
        [
            SalesRepTopSellerSortRule.Create(ByUnitsRuleName, "By units sold", SalesRepTopSellerSortBy.Units),
            SalesRepTopSellerSortRule.Create(ByRevenueRuleName, "By revenue", SalesRepTopSellerSortBy.Revenue),
        ]);

    public virtual async Task<SalesRepTopSellerCriteria> ApplySortAsync(string storeId, string sort, SalesRepTopSellerCriteria criteria)
    {
        var rule = await ResolveRuleAsync(storeId, sort);

        if (rule != null)
        {
            criteria.SortBy = rule.SortBy;
        }

        return criteria;
    }
}
