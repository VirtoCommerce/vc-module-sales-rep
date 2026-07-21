using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// The wire vocabulary for a sort direction — the "asc"/"desc" tokens the storefront sends and the module emits
/// into search-criteria sort expressions (e.g. "total:desc"). This is the formatting counterpart to the parse side
/// in <see cref="SortRuleResolverBase{TRule}"/>, kept in one place so the tokens are defined exactly once.
/// </summary>
public static class SortDirectionExtensions
{
    /// <summary>The lowercase token for <paramref name="direction"/>: "desc" for descending, "asc" otherwise.</summary>
    public static string ToToken(this SortDirection direction) =>
        direction == SortDirection.Descending ? "desc" : "asc";
}
