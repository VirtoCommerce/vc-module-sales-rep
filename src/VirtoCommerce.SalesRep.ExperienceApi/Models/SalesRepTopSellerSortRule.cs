using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepTopSellerSortRule : INamedSortRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public SalesRepTopSellerSortBy SortBy { get; set; }

    public SortDirection DefaultDirection { get; set; }

    public bool SupportsDirection { get; set; }

    public static SalesRepTopSellerSortRule Create(string name, string localizedName, SalesRepTopSellerSortBy sortBy, SortDirection defaultDirection, bool supportsDirection)
    {
        var result = AbstractTypeFactory<SalesRepTopSellerSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.SortBy = sortBy;
        result.DefaultDirection = defaultDirection;
        result.SupportsDirection = supportsDirection;
        return result;
    }
}
