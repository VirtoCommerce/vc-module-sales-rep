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
        var fields = includeFields ?? [];

        // Match on the leaf field name so the connection's own "totalCount" isn't mistaken for the order "total".
        bool Requested(string fieldName) =>
            fields.Any(x => !string.IsNullOrEmpty(x) && x.Split('.')[^1].Equals(fieldName, StringComparison.OrdinalIgnoreCase));

        // number/status/currency/createdDate are scalar columns loaded with Default — only total and itemsCount
        // opt into a heavier group.
        var result = CustomerOrderResponseGroup.Default;

        // total needs WithPrices — the order pipeline zeroes prices for lighter groups.
        if (Requested(nameof(SalesRepOrder.Total)))
        {
            result |= CustomerOrderResponseGroup.WithPrices;
        }

        // itemsCount needs the line items loaded.
        if (Requested(nameof(SalesRepOrder.ItemsCount)))
        {
            result |= CustomerOrderResponseGroup.WithItems;
        }

        return result.ToString();
    }
}
