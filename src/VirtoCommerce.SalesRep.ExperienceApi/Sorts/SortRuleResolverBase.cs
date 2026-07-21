using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// Base for the Sales Rep sort-rule resolvers: parses the client's sort argument (<c>"ruleName"</c> or
/// <c>"ruleName:asc|desc"</c>, X-Order style) and resolves it — once, over the abstract <see cref="GetRulesAsync"/> —
/// to a configured rule plus the concrete <see cref="SortDirection"/> to apply. Each domain resolver adds only its own
/// <c>Apply…</c>/<c>Resolve…</c> that maps the resolved (rule, direction) onto that domain's concrete ordering (a
/// search-criteria sort expression, an enum, or a richer spec).
///
/// Direction semantics are uniform across every list: no suffix → the rule's
/// <see cref="INamedSortRule.DefaultDirection"/>; a supported suffix → that direction; a valid suffix the rule does
/// not allow (<see cref="INamedSortRule.SupportsDirection"/> is false) → throw; a garbage suffix → ignored (default
/// direction). An empty or unknown rule NAME resolves to the first configured rule with its default direction, suffix
/// ignored — a sort only reorders, so it never fails closed on the name (only an unsupported <em>direction on a
/// recognized rule</em> is an error).
/// </summary>
/// <typeparam name="TRule">The selectable rule option (implements <see cref="INamedSortRule"/>).</typeparam>
public abstract class SortRuleResolverBase<TRule> : ISortRuleResolver<TRule>
    where TRule : class, INamedSortRule
{
    /// <inheritdoc />
    public abstract Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves <paramref name="sort"/> to a configured rule and the direction to apply. The rule is the one whose
    /// <see cref="INamedSortRule.Name"/> matches (case-insensitive) the part before an optional <c>:asc</c>/<c>:desc</c>
    /// suffix, or — when that part is empty or unrecognized — the first configured rule. The direction is honored only
    /// for a recognized rule; see the type summary for the full semantics. <c>Rule</c> is null only when no rules are
    /// configured.
    /// </summary>
    /// <exception cref="ArgumentException">The suffix names a valid direction the recognized rule does not allow.</exception>
    protected async Task<(TRule Rule, SortDirection Direction)> ResolveSortRuleAsync(string storeId, string sort)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        var (ruleName, directionToken) = ParseSort(sort);

        // Match by name; an empty/unknown name falls back to the first rule (a sort never fails closed on the name).
        var matched = string.IsNullOrEmpty(ruleName)
            ? null
            : rules.FirstOrDefault(x => string.Equals(x.Name, ruleName, StringComparison.OrdinalIgnoreCase));

        var rule = matched ?? rules.FirstOrDefault();
        if (rule == null)
        {
            return (null, SortDirection.Ascending);
        }

        // The direction suffix is honored only when the client named a RECOGNIZED rule; on a fallback it is ignored
        // entirely, so an unknown rule name never throws whatever its suffix.
        var direction = rule.DefaultDirection;
        if (matched != null && TryParseDirection(directionToken, out var requested))
        {
            if (requested != rule.DefaultDirection && !rule.SupportsDirection)
            {
                throw new ArgumentException(
                    $"Sort direction '{requested.ToToken()}' is not supported for sort rule '{rule.Name}'.",
                    nameof(sort));
            }

            direction = requested;
        }

        return (rule, direction);
    }

    /// <summary>
    /// Splits <c>"ruleName[:direction]"</c> on the single <c>':'</c> separator. Rule names contain no colon; the split
    /// is never on <c>'-'</c> (unlike <see cref="SortInfo"/>), because rule names such as <c>by-units</c> contain it.
    /// </summary>
    private static (string RuleName, string DirectionToken) ParseSort(string sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return (null, null);
        }

        var parts = sort.Split(':', 2);
        return parts.Length == 2
            ? (parts[0].Trim(), parts[1].Trim())
            : (parts[0].Trim(), null);
    }

    private static bool TryParseDirection(string token, out SortDirection direction)
    {
        if (string.Equals(token, "asc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Ascending;
            return true;
        }

        if (string.Equals(token, "desc", StringComparison.OrdinalIgnoreCase))
        {
            direction = SortDirection.Descending;
            return true;
        }

        direction = SortDirection.Ascending;
        return false;
    }
}
