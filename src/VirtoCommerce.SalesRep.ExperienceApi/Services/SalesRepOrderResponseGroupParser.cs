using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderResponseGroupParser : ISalesRepOrderResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        // number/status/currency/createdDate are scalar columns loaded with Default — only total and itemsCount
        // opt into a heavier group.
        var result = CustomerOrderResponseGroup.Default;

        // Match on the leaf field name so the connection's own "totalCount" isn't mistaken for the order "total".
        var leafFields = (includeFields ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x.Split('.')[^1])
            .ToArray();

        // total needs WithPrices — the order pipeline zeroes prices for lighter groups.
        if (leafFields.Contains(nameof(SalesRepOrder.Total), StringComparer.OrdinalIgnoreCase))
        {
            result |= CustomerOrderResponseGroup.WithPrices;
        }

        // itemsCount needs the line items loaded.
        if (leafFields.Contains(nameof(SalesRepOrder.ItemsCount), StringComparer.OrdinalIgnoreCase))
        {
            result |= CustomerOrderResponseGroup.WithItems;
        }

        return result.ToString();
    }
}
