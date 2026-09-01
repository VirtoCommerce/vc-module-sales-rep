using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

public abstract class SortRuleResolverBase<TRule> : ISortRuleResolver<TRule>
    where TRule : class, INamedSortRule
{
    public abstract Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);

    protected async Task<(TRule Rule, SortDirection Direction)> ResolveSortRuleAsync(string storeId, string sort)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);

        var (ruleName, directionToken) = ParseSort(sort);

        var matched = string.IsNullOrEmpty(ruleName)
            ? null
            : rules.FirstOrDefault(x => string.Equals(x.Name, ruleName, StringComparison.OrdinalIgnoreCase));

        var rule = matched ?? rules.FirstOrDefault();
        if (rule == null)
        {
            return (null, SortDirection.Ascending);
        }

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
    /// Writes the resolved rule onto search criteria as the platform's "field:direction" sort token. Here rather than
    /// in each resolver so the token format has one owner.
    /// </summary>
    protected async Task ApplyResolvedSortAsync(string storeId, string sort, SearchCriteriaBase criteria)
    {
        var (rule, direction) = await ResolveSortRuleAsync(storeId, sort);

        if (rule is IFieldSortRule fieldRule && !string.IsNullOrEmpty(fieldRule.SortField))
        {
            criteria.Sort = $"{fieldRule.SortField}:{direction.ToToken()}";
        }
    }

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
