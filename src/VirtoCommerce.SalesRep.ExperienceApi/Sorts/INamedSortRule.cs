namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// A selectable, named sort option surfaced to the storefront (recent orders, biggest orders, customer name,
/// last-order date, …). The client only ever sees and sends the <see cref="Name"/>; the server maps it to a concrete
/// ordering (see <see cref="ISortRuleResolver{TRule}"/>). Parallel to <c>INamedFilterRule</c> but a distinct axis —
/// filters choose <em>which</em> records, sorts choose their <em>order</em> — so the two are never crossed into one
/// combinatorial list.
/// </summary>
public interface INamedSortRule
{
    /// <summary>Stable rule id — the value the client sends back as the sort argument.</summary>
    string Name { get; set; }

    /// <summary>Localized label shown for the rule.</summary>
    string LocalizedName { get; set; }
}
