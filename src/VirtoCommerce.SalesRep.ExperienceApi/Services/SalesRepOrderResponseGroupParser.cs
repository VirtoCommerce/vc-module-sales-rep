using System.Collections.Generic;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderResponseGroupParser : ISalesRepOrderResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        var result = CustomerOrderResponseGroup.Default;

        if (includeFields.IncludesField(nameof(SalesRepOrder.Total)))
        {
            result |= CustomerOrderResponseGroup.WithPrices;
        }

        if (includeFields.IncludesField(nameof(SalesRepOrder.ItemsCount)))
        {
            result |= CustomerOrderResponseGroup.WithItems;
        }

        return result.ToString();
    }
}
