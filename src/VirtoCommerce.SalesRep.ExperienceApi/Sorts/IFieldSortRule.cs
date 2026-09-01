namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// A sort rule that maps onto a single index/database field, so it can be written to search criteria as the
/// platform's "field:direction" token. Rules that sort by something else — a computed ranking, say — do not
/// implement it and stay responsible for applying themselves.
/// </summary>
public interface IFieldSortRule : INamedSortRule
{
    string SortField { get; set; }
}
