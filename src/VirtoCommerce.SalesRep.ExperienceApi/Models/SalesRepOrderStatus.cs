using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepOrderStatus
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public IList<string> OrderStatuses { get; set; } = [];

    public static SalesRepOrderStatus Create(string name, string localizedName, params string[] orderStatuses)
    {
        var result = AbstractTypeFactory<SalesRepOrderStatus>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.OrderStatuses = orderStatuses;
        return result;
    }
}
