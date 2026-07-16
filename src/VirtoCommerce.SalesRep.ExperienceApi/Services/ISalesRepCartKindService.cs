using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep cart "kinds" shown as filter options, and the mapping that resolves selected kinds to the
/// underlying cart type/status filter to aggregate by. The cart analogue of <c>ISalesRepOrderStatusService</c>. The
/// default implementation exposes a single built-in "project" kind (cart type "Wishlist"); a platform-based project
/// replaces this service (DI last-registration wins) to hide, add or recompose kinds (e.g. an "active carts" kind).
/// </summary>
public interface ISalesRepCartKindService
{
    /// <summary>The selectable cart kinds (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<SalesRepCartKind>> GetKindsAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves the selected kinds (<see cref="SalesRepCartKind.Name"/>) to the deduped union of their underlying
    /// cart types and statuses. Returns an empty filter (<see cref="SalesRepCartFilter.IsEmpty"/>) when nothing is
    /// selected or the names are unknown, so the caller can apply no filter (nothing selected) or fail closed
    /// (names given but all unrecognized).
    /// </summary>
    Task<SalesRepCartFilter> ResolveCartFilterAsync(string storeId, IList<string> selectedKindNames);
}
