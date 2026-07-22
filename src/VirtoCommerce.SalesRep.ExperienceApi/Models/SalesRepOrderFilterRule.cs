using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepOrderFilterRule : INamedFilterRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public IList<string> OrderStatuses { get; set; } = [];

    public static SalesRepOrderFilterRule Create(string name, string localizedName, params string[] orderStatuses)
    {
        var result = AbstractTypeFactory<SalesRepOrderFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.OrderStatuses = orderStatuses;
        return result;
    }
}
