using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepOrderSortRule : IFieldSortRule
{
    public string Name { get; set; }

    public string LocalizedName { get; set; }

    public string SortField { get; set; }

    public SortDirection DefaultDirection { get; set; }

    public bool SupportsDirection { get; set; }

    public static SalesRepOrderSortRule Create(string name, string localizedName, string sortField, SortDirection defaultDirection, bool supportsDirection)
    {
        var result = AbstractTypeFactory<SalesRepOrderSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.SortField = sortField;
        result.DefaultDirection = defaultDirection;
        result.SupportsDirection = supportsDirection;
        return result;
    }
}
