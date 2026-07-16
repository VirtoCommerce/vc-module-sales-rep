namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// A selectable, named filter option surfaced to the storefront (an aggregated order status, a cart kind, a future
/// customer segment, …). The client only ever sees and sends the <see cref="Name"/>; the server maps it to a
/// concrete, engine-agnostic filter (see <see cref="IFilterRuleResolver{TRule,TFilter}"/>). Common shape so the
/// "rules list" queries and the statistics field-argument plumbing are written once across domains.
/// </summary>
public interface INamedFilterRule
{
    /// <summary>Stable rule id — the value the client sends back as the filter argument.</summary>
    string Name { get; set; }

    /// <summary>Localized label shown for the rule.</summary>
    string LocalizedName { get; set; }
}
