using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.Platform.Core.Common;
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

    public virtual Task<IList<SalesRepCartKind>> GetKindsAsync(string storeId, string cultureName)
    {
        IList<SalesRepCartKind> kinds =
        [
            SalesRepCartKind.Create(ProjectKind, "Projects", types: [ModuleConstants.CartType.Wishlist]),
        ];

        return Task.FromResult(kinds);
    }

    public virtual async Task<SalesRepCartFilter> ResolveCartFilterAsync(string storeId, IList<string> selectedKindNames)
    {
        var filter = AbstractTypeFactory<SalesRepCartFilter>.TryCreateInstance();

        if (selectedKindNames == null || selectedKindNames.Count == 0)
        {
            return filter;
        }

        var selected = new HashSet<string>(selectedKindNames, StringComparer.OrdinalIgnoreCase);

        var kinds = await GetKindsAsync(storeId, cultureName: null);
        var matched = kinds.Where(x => selected.Contains(x.Name)).ToList();

        filter.Types = matched
            .SelectMany(x => x.Types ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        filter.Statuses = matched
            .SelectMany(x => x.Statuses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return filter;
    }
}
