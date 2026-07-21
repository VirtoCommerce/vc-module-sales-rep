using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable ordering for the Sales Rep "My customers" list (VCST-5309): "my last orders" and "ytd purchases"
/// (order-derived) or "name" (a plain member column). A <see cref="INamedSortRule"/>: the client sends the
/// <see cref="Name"/>, the server maps it to a <see cref="SalesRepCustomerSortSpec"/> (via
/// <c>ISalesRepCustomerSortRuleResolver</c>) — a spec, not a bare sort string, because some orderings are computed
/// from the rep's orders, which the members search can't sort by. Extensible via a replacement resolver.
/// </summary>
public class SalesRepCustomerSortRule : INamedSortRule
{
    /// <summary>Stable sort-rule id — the value the client sends back as the <c>salesRepCustomers</c> "sort" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the ordering.</summary>
    public string LocalizedName { get; set; }

    /// <inheritdoc />
    public SortDirection DefaultDirection { get; set; }

    /// <inheritdoc />
    public bool AllowsReverse { get; set; }

    /// <summary>Constructs a rule via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepCustomerSortRule Create(string name, string localizedName, SortDirection defaultDirection, bool allowsReverse)
    {
        var result = AbstractTypeFactory<SalesRepCustomerSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.DefaultDirection = defaultDirection;
        result.AllowsReverse = allowsReverse;
        return result;
    }
}
