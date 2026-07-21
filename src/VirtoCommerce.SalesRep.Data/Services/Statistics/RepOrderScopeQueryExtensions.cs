using System.Linq;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Shared base scoping for the order-derived sales-rep aggregates (order statistics + "my customers" counts): the
/// rep's own (creator-scoped) non-cancelled, non-prototype orders within the served organizations and the optional
/// store. Callers layer the bits that differ per aggregate (status whitelist, date bounds) on top.
/// </summary>
internal static class RepOrderScopeQueryExtensions
{
    public static IQueryable<CustomerOrderEntity> ApplyRepScope(
        this IQueryable<CustomerOrderEntity> query,
        string[] organizationIds,
        string customerId,
        string storeId)
    {
        query = query.Where(x => !x.IsPrototype && !x.IsCancelled);

        if (!organizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => organizationIds.Contains(x.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only orders created by the calling sales rep — their user id
        // is recorded as the order's CustomerId, exactly as X-Order scopes "my orders".
        if (!string.IsNullOrEmpty(customerId))
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (!string.IsNullOrEmpty(storeId))
        {
            query = query.Where(x => x.StoreId == storeId);
        }

        return query;
    }

    /// <summary>
    /// The line-item counterpart of the order-query <c>ApplyRepScope</c>: scopes a line-item query to the rep's own
    /// non-cancelled line items of non-cancelled, non-prototype orders within the served organizations and optional
    /// store — applied through the line item's <c>CustomerOrder</c> navigation. Callers layer product/category/date
    /// filters on top.
    /// </summary>
    public static IQueryable<LineItemEntity> ApplyRepScope(
        this IQueryable<LineItemEntity> query,
        string[] organizationIds,
        string customerId,
        string storeId)
    {
        query = query.Where(x => !x.IsCancelled && !x.CustomerOrder.IsCancelled && !x.CustomerOrder.IsPrototype);

        if (!organizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => organizationIds.Contains(x.CustomerOrder.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only line items of orders the calling rep created.
        if (!string.IsNullOrEmpty(customerId))
        {
            query = query.Where(x => x.CustomerOrder.CustomerId == customerId);
        }

        if (!string.IsNullOrEmpty(storeId))
        {
            query = query.Where(x => x.CustomerOrder.StoreId == storeId);
        }

        return query;
    }
}
