using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

internal static class RepOrderScopeQueryExtensions
{
    public static IQueryable<CustomerOrderEntity> ApplyRepScope(
        this IQueryable<CustomerOrderEntity> query,
        IList<string> organizationIds,
        string customerId,
        string storeId)
    {
        query = query.Where(x => !x.IsPrototype && !x.IsCancelled);

        if (!organizationIds.IsNullOrEmpty())
        {
            query = query.Where(x => organizationIds.Contains(x.OrganizationId));
        }

        // Creator scoping (data-isolation invariant): only orders the calling rep created (their user id == CustomerId).
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

    public static IQueryable<LineItemEntity> ApplyRepScope(
        this IQueryable<LineItemEntity> query,
        IList<string> organizationIds,
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
