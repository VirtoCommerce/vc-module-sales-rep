using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepTaskFilterRule : INamedFilterRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public static SalesRepTaskFilterRule Create(string name, string localizedName)
    {
        var result = AbstractTypeFactory<SalesRepTaskFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        return result;
    }
}
