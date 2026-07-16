namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// The underlying cart filter a set of selected <see cref="SalesRepCartKind"/>s resolves to: the deduped union of
/// their cart types and statuses. Applied together (types AND statuses) when aggregating carts.
/// </summary>
public class SalesRepCartFilter
{
    /// <summary>Deduped union of the selected kinds' cart types (empty = don't filter by type).</summary>
    public string[] Types { get; set; } = [];

    /// <summary>Deduped union of the selected kinds' cart statuses (empty = don't filter by status).</summary>
    public string[] Statuses { get; set; } = [];

    /// <summary>
    /// True when the filter constrains nothing (no types and no statuses). Used for fail-closed handling: kind names
    /// that were supplied but resolved to an empty filter (all unrecognized) yield no data rather than every cart.
    /// </summary>
    public bool IsEmpty => (Types == null || Types.Length == 0) && (Statuses == null || Statuses.Length == 0);
}
