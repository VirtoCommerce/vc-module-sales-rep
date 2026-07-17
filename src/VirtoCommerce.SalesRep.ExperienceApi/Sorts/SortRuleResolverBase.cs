using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// Base for the Sales Rep sort-rule resolvers: resolves a selected rule name to a configured rule once, over the
/// abstract <see cref="GetRulesAsync"/>. Each domain resolver adds only its own <c>Apply…</c>/<c>Resolve…</c> that
/// maps the resolved rule onto that domain's concrete ordering (a search-criteria sort expression, an enum, or a
/// richer spec). Empty/unknown selections resolve to the first configured rule — a sort only reorders, so it never
/// fails closed (unlike a filter).
/// </summary>
/// <typeparam name="TRule">The selectable rule option (implements <see cref="INamedSortRule"/>).</typeparam>
public abstract class SortRuleResolverBase<TRule> : ISortRuleResolver<TRule>
    where TRule : class, INamedSortRule
{
    /// <inheritdoc />
    public abstract Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves <paramref name="sort"/> (a rule <see cref="INamedSortRule.Name"/>) to a configured rule: the rule
    /// whose name matches case-insensitively, or — when <paramref name="sort"/> is empty or unrecognized — the first
    /// configured rule. Null only when no rules are configured.
    /// </summary>
    protected async Task<TRule> ResolveRuleAsync(string storeId, string sort)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        var rule = string.IsNullOrEmpty(sort)
            ? null
            : rules.FirstOrDefault(x => string.Equals(x.Name, sort, StringComparison.OrdinalIgnoreCase));

        return rule ?? rules.FirstOrDefault();
    }
}
