using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default cart-kind source: a single built-in "project" kind mapped to the <see cref="ModuleConstants.CartType.Wishlist"/>
/// cart type (projects are wishlists in the Sales Rep paradigm). Deliberately filters by type only, not status — a
/// storefront cart's status is typically null, so a status filter would exclude real projects. Projects override
/// this service to add/hide/recompose kinds (e.g. an "active carts" kind, or an "active" project status set).
/// </summary>
public class SalesRepCartFilterRuleResolver : ISalesRepCartFilterRuleResolver
{
    /// <summary>The stable name of the built-in "project" (wishlist) kind.</summary>
    public const string ProjectKind = "project";

    public virtual Task<IList<SalesRepCartFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        IList<SalesRepCartFilterRule> kinds =
        [
            SalesRepCartFilterRule.Create(ProjectKind, "Projects", types: [ModuleConstants.CartType.Wishlist]),
        ];

        return Task.FromResult(kinds);
    }

    public virtual async Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerCartStatisticsCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria; // no filter → baseline
        }

        var kinds = await GetRulesAsync(storeId, cultureName: null);
        var kind = kinds.FirstOrDefault(x => string.Equals(x.Name, filter, StringComparison.OrdinalIgnoreCase));

        if (kind == null)
        {
            return null; // fail-closed: a rule name was given but is unrecognized
        }

        // A recognized kind with neither types nor statuses is an "all carts" rule → baseline (not fail-closed).
        if (kind.Types is { Length: > 0 })
        {
            criteria.Types = kind.Types;
        }

        if (kind.Statuses is { Length: > 0 })
        {
            criteria.Statuses = kind.Statuses;
        }

        return criteria;
    }
}
