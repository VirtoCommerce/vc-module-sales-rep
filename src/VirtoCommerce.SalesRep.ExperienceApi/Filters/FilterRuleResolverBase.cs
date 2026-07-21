using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// Base for the Sales Rep filter-rule resolvers: resolves a selected rule name to a configured rule once, over the
/// abstract <see cref="GetRulesAsync"/> — the filter counterpart of the sort side's <c>SortRuleResolverBase</c>, but
/// deliberately WITHOUT a fall-back-to-first: an unrecognized filter name must fail closed (the caller returns no
/// data), never silently widen to "everything". Each domain resolver keeps its own <c>Apply…</c> method that maps the
/// resolved rule onto that domain's concrete criteria (the criteria types and post-resolution work differ per domain).
/// </summary>
/// <typeparam name="TRule">The selectable rule option (implements <see cref="INamedFilterRule"/>).</typeparam>
public abstract class FilterRuleResolverBase<TRule> : IFilterRuleResolver<TRule>
    where TRule : class, INamedFilterRule
{
    /// <inheritdoc />
    public abstract Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves <paramref name="filter"/> (a rule <see cref="INamedFilterRule.Name"/>) to the configured rule whose
    /// name matches case-insensitively, or <c>null</c> when none matches. Routing through <see cref="GetRulesAsync"/>
    /// keeps the discovery list and the accepted filter values in lock-step — a subclass that hides or renames a rule
    /// via <see cref="GetRulesAsync"/> changes both at once. Callers treat an empty/omitted filter as the baseline (no
    /// narrowing) BEFORE calling this, and a <c>null</c> result as fail-closed.
    /// </summary>
    protected async Task<TRule> ResolveNamedRuleAsync(string storeId, string filter)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);
        return rules.FirstOrDefault(x => string.Equals(x.Name, filter, StringComparison.OrdinalIgnoreCase));
    }
}
