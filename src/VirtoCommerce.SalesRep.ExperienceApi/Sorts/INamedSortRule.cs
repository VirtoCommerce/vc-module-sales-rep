using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

/// <summary>
/// A selectable, named sort option surfaced to the storefront (recent orders, biggest orders, customer name,
/// last-order date, …). The client sends the <see cref="Name"/> — optionally with a trailing <c>:asc</c>/<c>:desc</c>
/// direction suffix (X-Order style) — and the server maps it to a concrete ordering (see
/// <see cref="ISortRuleResolver{TRule}"/>). Parallel to <c>INamedFilterRule</c> but a distinct axis — filters choose
/// <em>which</em> records, sorts choose their <em>order</em> — so the two are never crossed into one combinatorial list.
/// </summary>
public interface INamedSortRule
{
    /// <summary>Stable rule id — the value the client sends back as the sort argument (optionally with a <c>:asc</c>/<c>:desc</c> suffix).</summary>
    string Name { get; set; }

    /// <summary>Localized label shown for the rule.</summary>
    string LocalizedName { get; set; }

    /// <summary>The direction applied when the client sends the rule name with no (or an unrecognized) direction suffix.</summary>
    SortDirection DefaultDirection { get; set; }

    /// <summary>
    /// Whether the client may choose the sort direction for this rule — i.e. the opposite of
    /// <see cref="DefaultDirection"/> is also a meaningful ordering. When false, an explicit opposite-direction suffix
    /// (e.g. <c>recent:asc</c>) is rejected rather than silently ignored.
    /// </summary>
    bool SupportsDirection { get; set; }
}
