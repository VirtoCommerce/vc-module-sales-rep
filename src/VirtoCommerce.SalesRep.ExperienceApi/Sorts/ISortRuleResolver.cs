using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// The shared part of every Sales Rep sort-rule source (order orderings, customer orderings, …): the selectable,
/// named options. Each domain resolver additionally exposes an <c>Apply…</c>/<c>Resolve…</c> method that maps a
/// selection onto that domain's concrete ordering (a search-criteria sort expression, or a richer spec when the
/// ordering is derived from another store the criteria can't express). Those live on the domain interface — not
/// here — because the shapes differ per domain. Unknown/empty selections resolve to the domain's default ordering
/// (a sort only reorders; it is never a data-scope decision, so — unlike a filter — it never fails closed). The
/// default implementation is registered per domain and is DI-overridable (last registration wins).
/// </summary>
/// <typeparam name="TRule">The selectable rule option (implements <see cref="INamedSortRule"/>).</typeparam>
public interface ISortRuleResolver<TRule>
    where TRule : INamedSortRule
{
    /// <summary>The selectable sort rules (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);
}
