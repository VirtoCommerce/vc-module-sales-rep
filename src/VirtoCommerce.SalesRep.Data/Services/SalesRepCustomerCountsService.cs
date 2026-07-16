using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Computes the "my customers" counters from the rep's own orders (dashboard "My Customers" widget). Reads the
/// Orders EF store (<see cref="IOrderRepository"/>) directly — the same scoped .Data exception as the order/cart
/// statistics services — to count distinct organizations DB-side rather than loading orders into memory.
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

        var scoped = ApplyScope(repository.CustomerOrders, criteria);

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

        // Customers whose first-ever order by the rep falls in the range: group all the rep's orders per organization,
        // take each organization's earliest order date, then count those landing in [FromDate, ToDate).
        var firstOrderDates = await scoped
            .GroupBy(x => x.OrganizationId)
            .Select(g => g.Min(x => x.CreatedDate))
            .ToListAsync();

        var newCustomers = firstOrderDates.Count(first =>
            (criteria.FromDate == null || first >= criteria.FromDate.Value) &&
            (criteria.ToDate == null || first < criteria.ToDate.Value));

        var result = AbstractTypeFactory<SalesRepCustomerCountsPeriod>.TryCreateInstance();
        result.OrderingCustomers = orderingCustomers;
        result.NewCustomers = newCustomers;
        return result;
    }

    // The shared filter for both counters: the rep's own (creator-scoped) non-cancelled, non-prototype orders within
    // the served organizations and optional store. Date bounds are applied by the caller (they differ per counter).
    private static IQueryable<CustomerOrderEntity> ApplyScope(IQueryable<CustomerOrderEntity> query, SalesRepCustomerCountsCriteria criteria)
    {
        query = query.Where(x => !x.IsPrototype && !x.IsCancelled);

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
