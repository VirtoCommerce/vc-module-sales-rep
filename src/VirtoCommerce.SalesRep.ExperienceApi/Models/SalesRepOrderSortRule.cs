using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable ordering for the Sales Rep orders list (VCST-5309): "recent" (newest first) and, when a project adds
/// them, "biggest by total" etc. A <see cref="INamedSortRule"/>: the client sends the <see cref="Name"/>, the server
/// maps it to the order search's <see cref="Sort"/> expression (via <c>ISalesRepOrderSortRuleResolver</c>).
/// Extensible via a replacement resolver and/or <c>AbstractTypeFactory.OverrideType</c>.
/// </summary>
public class SalesRepOrderSortRule : INamedSortRule
{
    /// <summary>Stable sort-rule id — the value the client sends back as the <c>salesRepOrders</c> "sort" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the ordering.</summary>
    public string LocalizedName { get; set; }

    /// <summary>The order search-criteria sort expression this rule maps to (e.g. "createdDate:desc").</summary>
    public string Sort { get; set; }

    /// <summary>Constructs a rule via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepOrderSortRule Create(string name, string localizedName, string sort)
    {
        var result = AbstractTypeFactory<SalesRepOrderSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.Sort = sort;
        return result;
    }
}
