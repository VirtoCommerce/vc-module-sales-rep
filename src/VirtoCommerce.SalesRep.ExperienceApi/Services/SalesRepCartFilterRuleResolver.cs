using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default cart-kind source: a single built-in "active-carts" kind — non-empty carts that are not projects. It
/// excludes the <see cref="ModuleConstants.CartType.Wishlist"/> type (projects are wishlists in the Sales Rep paradigm)
/// and counts only carts with at least one line item, so a cart emptied by placing its order stops counting.
/// Deliberately does not filter by status — a storefront cart's status is typically null. Projects override this
/// service to add/hide/recompose kinds (e.g. a "project" wishlist kind, or an "active" status set).
/// </summary>
public class SalesRepCartFilterRuleResolver : FilterRuleResolverBase<SalesRepCartFilterRule>, ISalesRepCartFilterRuleResolver
{
    /// <summary>The stable name of the built-in "active carts" (non-empty, non-project) kind.</summary>
    public const string ActiveCartsKind = "active-carts";

    public override Task<IList<SalesRepCartFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        IList<SalesRepCartFilterRule> kinds =
        [
            SalesRepCartFilterRule.Create(
                ActiveCartsKind,
                "Active carts",
                excludeTypes: [ModuleConstants.CartType.Wishlist],
                onlyNonEmpty: true),
        ];

        return Task.FromResult(kinds);
    }

    public virtual async Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerCartStatisticsCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria; // no filter → baseline
        }

        var kind = await ResolveNamedRuleAsync(storeId, filter);

        if (kind == null)
        {
            return null; // fail-closed: a rule name was given but is unrecognized
        }

        // A recognized kind with no filters at all is an "all carts" rule → baseline (not fail-closed).
        if (kind.Types is { Count: > 0 })
        {
            criteria.Types = kind.Types;
        }

        if (kind.ExcludeTypes is { Count: > 0 })
        {
            criteria.ExcludeTypes = kind.ExcludeTypes;
        }

        if (kind.Statuses is { Count: > 0 })
        {
            criteria.Statuses = kind.Statuses;
        }

        criteria.OnlyNonEmpty = kind.OnlyNonEmpty;

        return criteria;
    }
}
