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
public class SalesRepCartKindService : ISalesRepCartKindService
{
    /// <summary>The stable name of the built-in "project" (wishlist) kind.</summary>
    public const string ProjectKind = "project";

    public virtual Task<IList<SalesRepCartKind>> GetRulesAsync(string storeId, string cultureName)
    {
        IList<SalesRepCartKind> kinds =
        [
            SalesRepCartKind.Create(ProjectKind, "Projects", types: [ModuleConstants.CartType.Wishlist]),
        ];

        return Task.FromResult(kinds);
    }

    public virtual async Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, IList<string> selectedNames, CustomerCartStatisticsCriteria criteria)
    {
        if (selectedNames == null || selectedNames.Count == 0)
        {
            return criteria; // no filter
        }

        var selected = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);

        var kinds = await GetRulesAsync(storeId, cultureName: null);
        var matched = kinds.Where(x => selected.Contains(x.Name)).ToList();

        var types = matched.SelectMany(x => x.Types ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var statuses = matched.SelectMany(x => x.Statuses ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (types.Length == 0 && statuses.Length == 0)
        {
            return null; // fail-closed: kinds selected but none recognized
        }

        if (types.Length > 0)
        {
            criteria.Types = types;
        }

        if (statuses.Length > 0)
        {
            criteria.Statuses = statuses;
        }

        return criteria;
    }
}
