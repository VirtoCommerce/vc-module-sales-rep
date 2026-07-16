using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// The shared part of every Sales Rep filter-rule source (order statuses, cart kinds, future customer segments): the
/// selectable, named options. Each domain resolver additionally exposes <c>Apply…FilterAsync</c> methods that map a
/// selection onto that domain's concrete criteria (search criteria for a list, aggregation criteria for statistics).
/// Those live on the domain interface — not here — because the criteria types differ per domain, and keeping every
/// mapping of a domain in one class is what stops the list and the statistics from drifting. Each apply method
/// returns <c>null</c> when names were supplied but none resolved (fail-closed), so callers never inspect concrete
/// filter fields. The default implementation is registered per domain and is DI-overridable (last registration wins).
/// </summary>
/// <typeparam name="TRule">The selectable rule option (implements <see cref="INamedFilterRule"/>).</typeparam>
public interface IFilterRuleResolver<TRule>
    where TRule : INamedFilterRule
{
    /// <summary>The selectable rules (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);
}
