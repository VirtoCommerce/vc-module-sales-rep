using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable order status for the Sales Rep orders filter (VCST-5308). May be a single order status or a
/// composite grouping several underlying <see cref="OrderStatuses"/> (e.g. "Not active" → Cancelled + Failed).
/// A <see cref="INamedFilterRule"/>: the client sends the <see cref="Name"/>, the server resolves it and applies it
/// to the order criteria (via <c>ISalesRepOrderFilterRuleResolver</c>). Extensible: a project registers its own service
/// and/or overrides this type via <c>AbstractTypeFactory.OverrideType</c> to add, hide or compose statuses.
/// </summary>
public class SalesRepOrderFilterRule : INamedFilterRule
{
    /// <summary>Stable status id — the value the client sends back as the <c>salesRepOrders</c> "status" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the status.</summary>
    public string LocalizedName { get; set; }

    /// <summary>Underlying order statuses this status maps to (used to filter orders). A single value for a 1:1 status.</summary>
    public string[] OrderStatuses { get; set; } = [];

    /// <summary>Constructs a status via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepOrderFilterRule Create(string name, string localizedName, params string[] orderStatuses)
    {
        var result = AbstractTypeFactory<SalesRepOrderFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.OrderStatuses = orderStatuses;
        return result;
    }
}
