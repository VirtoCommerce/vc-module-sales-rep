using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "My customers" ordering source: "my last orders" (default — newest rep order first), "ytd purchases"
/// (this year's order total, biggest first) and "name" (organization name A→Z). The first two are order-derived
/// (the handler ranks by the per-organization order aggregate); "name" is a plain member-column sort. Both the
/// default and any unrecognized selection resolve to the first rule. Projects override this service to add orderings.
/// </summary>
public class SalesRepCustomerSortRuleResolver : ISalesRepCustomerSortRuleResolver
{
    /// <summary>Newest rep-created order first (the default ordering).</summary>
    public const string MyLastOrdersRuleName = "my-last-orders";

    /// <summary>Largest year-to-date purchase total first (order-derived).</summary>
    public const string YtdPurchasesRuleName = "ytd-purchases";

    /// <summary>Organization name, A→Z (a plain member column).</summary>
    public const string NameRuleName = "name";

    public virtual Task<IList<SalesRepCustomerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerSortRule>>(
        [
            SalesRepCustomerSortRule.Create(MyLastOrdersRuleName, "My last orders"),
            SalesRepCustomerSortRule.Create(YtdPurchasesRuleName, "YTD purchases"),
            SalesRepCustomerSortRule.Create(NameRuleName, "Customer name"),
        ]);

    public virtual async Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        // Default / unrecognized → the first configured rule (a sort never fails closed).
        var name = !string.IsNullOrEmpty(sort) && rules.Any(x => string.Equals(x.Name, sort, StringComparison.OrdinalIgnoreCase))
            ? sort
            : rules.FirstOrDefault()?.Name;

        return BuildSpec(name);
    }

    /// <summary>Maps a recognized rule name to its ordering spec. Override to map additional custom rules.</summary>
    protected virtual SalesRepCustomerSortSpec BuildSpec(string ruleName)
    {
        if (string.Equals(ruleName, NameRuleName, StringComparison.OrdinalIgnoreCase))
        {
            return new SalesRepCustomerSortSpec { IsOrderDerived = false, MemberSort = "name:asc", Descending = false };
        }

        if (string.Equals(ruleName, YtdPurchasesRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return new SalesRepCustomerSortSpec { IsOrderDerived = true, Metric = SalesRepCustomerSortMetric.Total, FromDate = startOfYear };
        }

        // Default: my last orders (most recent rep order first).
        return new SalesRepCustomerSortSpec { IsOrderDerived = true, Metric = SalesRepCustomerSortMetric.LastOrderDate };
    }
}
