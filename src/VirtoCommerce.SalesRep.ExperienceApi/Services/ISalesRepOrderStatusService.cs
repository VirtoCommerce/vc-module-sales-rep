using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep order statuses shown as filter options, and the mapping that resolves selected statuses
/// to the underlying order statuses to filter by. The default implementation exposes each configured
/// <c>Order.Status</c> value as its own option (1:1). A platform-based project replaces this service (DI
/// last-registration wins) to hide, add or compose statuses (e.g. a "Not active" option resolving to
/// Cancelled + Failed).
/// </summary>
public interface ISalesRepOrderStatusService
{
    /// <summary>The selectable order statuses (in display order) for <paramref name="storeId"/>, labels localized to <paramref name="cultureName"/>.</summary>
    Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName);

    /// <summary>
    /// Resolves the selected statuses (<see cref="SalesRepOrderStatus.Name"/>) to the deduped union of their
    /// underlying order statuses to filter by. Returns an empty array when nothing is selected or the names are
    /// unknown, so the caller applies no status filter (the orders query only filters when the result is non-empty).
    /// </summary>
    Task<string[]> ResolveOrderStatusesAsync(string storeId, IList<string> selectedStatusNames);
}
