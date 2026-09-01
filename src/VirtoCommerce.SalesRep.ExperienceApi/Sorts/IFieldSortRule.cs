namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

// A sort rule that maps onto one field, so the shared base can write it as a "field:direction" token. A rule
// that sorts by something else stays responsible for applying itself.
public interface IFieldSortRule : INamedSortRule
{
    string SortField { get; set; }
}
