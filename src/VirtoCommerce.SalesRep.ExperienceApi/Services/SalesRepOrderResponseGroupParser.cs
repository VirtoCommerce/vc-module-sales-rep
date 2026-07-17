using System.Collections.Generic;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderResponseGroupParser : ISalesRepOrderResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        // number/status/currency/createdDate are scalar columns loaded with Default; only total and itemsCount opt
        // into a heavier group. IncludesField matches on any path segment (not just the leaf), so `total { amount }`
        // is recognized by its "total" segment while the connection's own "totalCount" segment never is.
        var result = CustomerOrderResponseGroup.Default;

        // total needs WithPrices — the order pipeline zeroes prices for lighter groups.
        if (includeFields.IncludesField(nameof(SalesRepOrder.Total)))
        {
            result |= CustomerOrderResponseGroup.WithPrices;
        }

        // itemsCount / itemsQuantity need the line items loaded.
        if (includeFields.IncludesField(nameof(SalesRepOrder.ItemsCount)) ||
            includeFields.IncludesField(nameof(SalesRepOrder.ItemsQuantity)))
        {
            result |= CustomerOrderResponseGroup.WithItems;
        }

        return result.ToString();
    }
}
