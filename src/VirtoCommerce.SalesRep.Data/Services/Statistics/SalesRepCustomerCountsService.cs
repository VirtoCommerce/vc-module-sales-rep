using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services.Statistics;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Computes the "my customers" counters (dashboard "My Customers" widget). "Ordering customers" is counted DB-side
/// over the rep's own orders — the Orders EF store (<see cref="IOrderRepository"/>) is read directly, the same scoped
/// .Data exception as the order/cart statistics services, rather than loading orders into memory. "New customers" is
/// counted from the assignment dates the handler supplies on the criteria (when each served customer was assigned to
/// the rep), so it reflects recent assignments rather than the customer's first order or creation date.
/// </summary>
public class SalesRepCustomerCountsService : ISalesRepCustomerCountsService
{
    private readonly Func<IOrderRepository> _orderRepositoryFactory;

    public SalesRepCustomerCountsService(Func<IOrderRepository> orderRepositoryFactory)
    {
        _orderRepositoryFactory = orderRepositoryFactory;
    }

    public virtual async Task<SalesRepCustomerCountsPeriod> GetCountsAsync(SalesRepCustomerCountsCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        using var repository = _orderRepositoryFactory();

        var scoped = BuildQuery(repository, criteria);

        // Customers the rep ordered for within the range.
        var inRange = scoped;
        if (criteria.FromDate != null)
        {
            inRange = inRange.Where(x => x.CreatedDate >= criteria.FromDate.Value);
        }

        if (criteria.ToDate != null)
        {
            inRange = inRange.Where(x => x.CreatedDate < criteria.ToDate.Value);
        }

        var orderingCustomers = await inRange
            .Select(x => x.OrganizationId)
            .Distinct()
            .CountAsync();

        // Customers newly assigned to the rep within the range: count the per-organization assignment dates the handler
        // resolved from the rep's granting memberships that land in [FromDate, ToDate). This is an in-memory count over a
        // bounded set (one date per served organization) — assignment is independent of orders, so no order query.
        var assignmentDates = criteria.AssignmentDates ?? [];
        var newCustomers = assignmentDates.Count(assigned =>
            (criteria.FromDate == null || assigned >= criteria.FromDate.Value) &&
            (criteria.ToDate == null || assigned < criteria.ToDate.Value));

        var result = AbstractTypeFactory<SalesRepCustomerCountsPeriod>.TryCreateInstance();
        result.OrderingCustomers = orderingCustomers;
        result.NewCustomers = newCustomers;
        return result;
    }

    /// <summary>
    /// The base order query both counters build on: the rep's own (creator-scoped) non-cancelled, non-prototype
    /// orders within the served organizations and optional store. Date bounds are applied by the caller (they differ
    /// per counter). The extension seam for a customer-segment rule the standard criteria can't express — a project
    /// subclasses this service, calls <c>base</c>, and appends its segment predicate when it recognizes a flag on its
    /// own <see cref="SalesRepCustomerCountsCriteria"/> subclass (paired with an <c>ISalesRepCustomerFilterRuleResolver</c>).
    /// </summary>
    protected virtual IQueryable<CustomerOrderEntity> BuildQuery(IOrderRepository repository, SalesRepCustomerCountsCriteria criteria)
    {
        var query = repository.CustomerOrders.Where(x => !x.IsPrototype && !x.IsCancelled);

        if (!criteria.OrganizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.OrganizationIds.Contains(x.OrganizationId));
        }

        if (!string.IsNullOrEmpty(criteria.CustomerId))
        {
            query = query.Where(x => x.CustomerId == criteria.CustomerId);
        }

        if (!string.IsNullOrEmpty(criteria.StoreId))
        {
            query = query.Where(x => x.StoreId == criteria.StoreId);
        }

        return query;
    }
}
