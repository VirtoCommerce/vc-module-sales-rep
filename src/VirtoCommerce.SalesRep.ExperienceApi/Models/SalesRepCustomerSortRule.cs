using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerSortRule : INamedSortRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public SortDirection DefaultDirection { get; set; }

    public bool SupportsDirection { get; set; }

    public static SalesRepCustomerSortRule Create(string name, string localizedName, SortDirection defaultDirection, bool supportsDirection)
    {
        var result = AbstractTypeFactory<SalesRepCustomerSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.DefaultDirection = defaultDirection;
        result.SupportsDirection = supportsDirection;
        return result;
    }
}
