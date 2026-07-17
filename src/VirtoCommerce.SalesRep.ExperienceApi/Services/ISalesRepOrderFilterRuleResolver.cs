using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Source of the Sales Rep order statuses (filter options) and the single mapping that applies a selection to the
/// order criteria. Both readers of the order domain apply through this one service — the <c>salesRepOrders</c> list
/// (<see cref="ApplyListFilterAsync"/>) and the order statistics (<see cref="ApplyStatisticsFilterAsync"/>) — so a
/// status name means exactly the same thing in both, and both mappings live in one class (they can't drift). The
/// default implementation exposes each configured <c>Order.Status</c> value as its own option (1:1); a project
/// replaces this service (DI last-registration wins) to hide, add or compose statuses (e.g. "Not active" →
/// Cancelled + Failed).
/// </summary>
public interface ISalesRepOrderFilterRuleResolver : IFilterRuleResolver<SalesRepOrderFilterRule>
{
    /// <summary>
    /// Applies the selected statuses to the orders-list search criteria and returns it. Returns the criteria
    /// unchanged when no statuses were selected, and <c>null</c> when statuses were selected but none resolved
    /// (all unrecognized for the store) — fail-closed, so the caller yields no orders rather than every order.
    /// </summary>
    Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, IList<string> selectedNames, CustomerOrderSearchCriteria criteria);

    /// <summary>The same status resolution applied to the order-statistics criteria. <c>null</c> = fail-closed.</summary>
    Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, IList<string> selectedNames, CustomerOrderStatisticsCriteria criteria);
}
