using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "My customers" ordering source: "my last orders" (default — newest rep order first), "ytd purchases"
/// (this year's order total, biggest first) and "name" (organization name A→Z). All three are reversible with a
/// <c>:asc</c>/<c>:desc</c> suffix. The first two are order-derived (the handler ranks by the per-organization order
/// aggregate); "name" is a plain member-column sort. Both the default and any unrecognized selection resolve to the
/// first rule. Projects override this service to add orderings.
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
            SalesRepCustomerSortRule.Create(MyLastOrdersRuleName, "My last orders", SortDirection.Descending, supportsDirection: true),
            SalesRepCustomerSortRule.Create(YtdPurchasesRuleName, "YTD purchases", SortDirection.Descending, supportsDirection: true),
            SalesRepCustomerSortRule.Create(NameRuleName, "Customer name", SortDirection.Ascending, supportsDirection: true),
        ]);

    public virtual async Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort)
    {
        // The base parses the rule name + optional :asc/:desc suffix and yields the direction to apply (the rule's
        // natural default unless the client asked for — and the rule allows — the opposite).
        var (rule, direction) = await ResolveSortRuleAsync(storeId, sort);

        var spec = BuildSpec(rule?.Name);
        spec.Descending = direction == SortDirection.Descending;
        return spec;
    }

    /// <summary>
    /// Maps a recognized rule name to its ordering spec — the field/metric/window the rule determines. The direction
    /// is applied by <see cref="ResolveSortAsync"/> from the parsed suffix, so this sets no direction. Override to map
    /// additional custom rules.
    /// </summary>
    protected virtual SalesRepCustomerSortSpec BuildSpec(string ruleName)
    {
        if (string.Equals(ruleName, NameRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var nameSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            nameSpec.IsOrderDerived = false;
            nameSpec.MemberSortField = "name";
            return nameSpec;
        }

        if (string.Equals(ruleName, YtdPurchasesRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ytdSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            ytdSpec.IsOrderDerived = true;
            ytdSpec.Metric = SalesRepCustomerSortMetric.Total;
            ytdSpec.FromDate = startOfYear;
            return ytdSpec;
        }

        // Default: my last orders (most recent rep order first).
        var defaultSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
        defaultSpec.IsOrderDerived = true;
        defaultSpec.Metric = SalesRepCustomerSortMetric.LastOrderDate;
        return defaultSpec;
    }
}
