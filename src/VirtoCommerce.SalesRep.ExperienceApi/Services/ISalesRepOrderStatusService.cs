using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep order statuses shown as filter options, plus the mapping used to (a) resolve a selected
/// status to the underlying order statuses to filter by and (b) localize an order's raw status for display.
/// The default implementation exposes each configured <c>Order.Status</c> value as its own option (1:1) and
/// localizes straight from the order-status settings dictionary. A platform-based project replaces this service
/// (DI last-registration wins) to hide, add or compose statuses (e.g. a "Not active" option resolving to
/// Cancelled + Failed) and to control how statuses are localized.
/// </summary>
public interface ISalesRepOrderStatusService
{
    /// <summary>The selectable order statuses (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves a selected status (<see cref="SalesRepOrderStatus.Name"/>) to the underlying order statuses to
    /// filter by. Returns an empty array when <paramref name="selectedStatusName"/> is null or unknown, so the
    /// caller applies no status filter (the orders query only filters when the result is non-empty).
    /// </summary>
    Task<string[]> ResolveOrderStatusesAsync(string storeId, string selectedStatusName);

    /// <summary>
    /// The localized labels of the configured order statuses, keyed by raw order status (case-insensitive) — used
    /// to fill an order's localized status for display. Statuses without a configured label are omitted.
    /// </summary>
    Task<IDictionary<string, string>> GetLocalizedStatusesAsync(string storeId, string cultureName);
}
