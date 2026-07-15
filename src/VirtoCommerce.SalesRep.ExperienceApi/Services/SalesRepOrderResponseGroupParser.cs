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

        // Match on any path segment (not just the leaf) so an object-valued field like `total { amount }` — whose
        // leaf is "amount" — is still recognized by its "total" segment, while the connection's own "totalCount"
        // (a different segment) is never mistaken for the order "total".
        bool Requested(string fieldName) =>
            fields.Any(path => !string.IsNullOrEmpty(path)
                && path.Split('.').Any(segment => segment.Equals(fieldName, StringComparison.OrdinalIgnoreCase)));

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
