using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

public static class SortDirectionExtensions
{
    public static string ToToken(this SortDirection direction) =>
        direction == SortDirection.Descending ? "desc" : "asc";
}
