using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "My customers" ordering source: "my last orders" (default — newest rep order first), "ytd purchases"
/// (this year's order total, biggest first) and "name" (organization name A→Z). The first two are order-derived
/// (the handler ranks by the per-organization order aggregate); "name" is a plain member-column sort. Both the
/// default and any unrecognized selection resolve to the first rule. Projects override this service to add orderings.
/// </summary>
public class SalesRepCustomerSortRuleResolver : SortRuleResolverBase<SalesRepCustomerSortRule>, ISalesRepCustomerSortRuleResolver
{
    /// <summary>Newest rep-created order first (the default ordering).</summary>
    public const string MyLastOrdersRuleName = "my-last-orders";

    /// <summary>Largest year-to-date purchase total first (order-derived).</summary>
    public const string YtdPurchasesRuleName = "ytd-purchases";

    /// <summary>Organization name, A→Z (a plain member column).</summary>
    public const string NameRuleName = "name";

    public override Task<IList<SalesRepCustomerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerSortRule>>(
        [
            SalesRepCustomerSortRule.Create(MyLastOrdersRuleName, "My last orders"),
            SalesRepCustomerSortRule.Create(YtdPurchasesRuleName, "YTD purchases"),
            SalesRepCustomerSortRule.Create(NameRuleName, "Customer name"),
        ]);

    public virtual async Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort, bool? descending)
    {
        var rule = await ResolveRuleAsync(storeId, sort);
        var spec = BuildSpec(rule?.Name);

        // An explicit direction (when the client sent one) overrides the rule's natural default — so any field can be
        // ordered either way (name Z→A, purchases/last-order smallest/oldest first).
        if (descending.HasValue)
        {
            spec.Descending = descending.Value;
        }

        return spec;
    }

    /// <summary>Maps a recognized rule name to its ordering spec, with the rule's natural direction. Override to map additional custom rules.</summary>
    protected virtual SalesRepCustomerSortSpec BuildSpec(string ruleName)
    {
        if (string.Equals(ruleName, NameRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var nameSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            nameSpec.IsOrderDerived = false;
            nameSpec.MemberSortField = "name";
            nameSpec.Descending = false; // name is A→Z by default
            return nameSpec;
        }

        if (string.Equals(ruleName, YtdPurchasesRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ytdSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            ytdSpec.IsOrderDerived = true;
            ytdSpec.Metric = SalesRepCustomerSortMetric.Total;
            ytdSpec.FromDate = startOfYear;
            ytdSpec.Descending = true; // biggest purchases first by default
            return ytdSpec;
        }

        // Default: my last orders (most recent rep order first).
        var defaultSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
        defaultSpec.IsOrderDerived = true;
        defaultSpec.Metric = SalesRepCustomerSortMetric.LastOrderDate;
        defaultSpec.Descending = true; // newest order first by default
        return defaultSpec;
    }
}
