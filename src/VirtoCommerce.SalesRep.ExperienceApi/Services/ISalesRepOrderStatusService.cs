using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep order-status tabs and the tab → underlying-statuses mapping used to filter orders.
/// The default implementation exposes each configured <c>Order.Status</c> value as its own tab (1:1). A
/// platform-based project replaces this service (DI last-registration wins) to hide, add or compose statuses
/// (e.g. a "Not active" tab that resolves to Cancelled + Failed) — which is also how it controls the filter.
/// </summary>
public interface ISalesRepOrderStatusService
{
    /// <summary>The status tabs (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves a selected tab (<see cref="SalesRepOrderStatus.Name"/>) to the underlying order statuses to filter by.
    /// Returns an empty array when <paramref name="selectedStatusName"/> is null or unknown, so the caller applies
    /// no status filter (the orders query only filters when the result is non-empty).
    /// </summary>
    Task<string[]> ResolveOrderStatusesAsync(string storeId, string selectedStatusName);
}
