using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

public interface INamedSortRule
{
    string Name { get; set; }

    string LocalizedName { get; set; }

    SortDirection DefaultDirection { get; set; }

    bool SupportsDirection { get; set; }
}
