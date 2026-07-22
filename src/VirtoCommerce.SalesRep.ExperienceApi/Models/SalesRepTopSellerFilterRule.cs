using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepTopSellerFilterRule : INamedFilterRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public static SalesRepTopSellerFilterRule Create(string name, string localizedName)
    {
        var result = AbstractTypeFactory<SalesRepTopSellerFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        return result;
    }
}
