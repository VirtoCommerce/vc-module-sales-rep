using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable order-status tab for the Sales Rep orders panel (VCST-5308). May be a single order status or a
/// composite grouping several underlying <see cref="OrderStatuses"/> (e.g. "Not active" → Cancelled + Failed).
/// Extensible: a project registers its own <c>ISalesRepOrderStatusService</c> and/or overrides this type via
/// <c>AbstractTypeFactory.OverrideType</c> to add, hide or compose statuses.
/// </summary>
public class SalesRepOrderStatus
{
    /// <summary>Stable status id — the tab key the client sends back as the <c>salesRepOrders</c> "status" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown on the tab / status badge.</summary>
    public string LocalizedName { get; set; }

    /// <summary>Underlying order statuses this tab maps to (used to filter orders). A single value for a 1:1 status.</summary>
    public string[] OrderStatuses { get; set; } = [];

    /// <summary>Constructs a status via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepOrderStatus Create(string name, string localizedName, params string[] orderStatuses)
    {
        var result = AbstractTypeFactory<SalesRepOrderStatus>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.OrderStatuses = orderStatuses;
        return result;
    }
}
