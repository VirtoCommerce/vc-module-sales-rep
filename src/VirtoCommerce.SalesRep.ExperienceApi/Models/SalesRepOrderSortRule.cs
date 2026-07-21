using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable ordering for the Sales Rep orders list (VCST-5309): "recent" (newest first) and "total" (by order
/// value; biggest first by default, reversible to smallest first with a <c>:asc</c> suffix). A
/// <see cref="INamedSortRule"/>: the client sends the <see cref="Name"/> (optionally with a <c>:asc</c>/<c>:desc</c>
/// suffix), the server maps it to the order search's sort expression — <see cref="SortField"/> plus the resolved
/// direction — via <c>ISalesRepOrderSortRuleResolver</c>. Extensible via a replacement resolver and/or
/// <c>AbstractTypeFactory.OverrideType</c>.
/// </summary>
public class SalesRepOrderSortRule : INamedSortRule
{
    /// <summary>Stable sort-rule id — the value the client sends back as the <c>salesRepOrders</c> "sort" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the ordering.</summary>
    public string LocalizedName { get; set; }

    /// <summary>The order search-criteria sort COLUMN this rule maps to (e.g. "createdDate", "total"); the resolver appends the resolved direction.</summary>
    public string SortField { get; set; }

    /// <inheritdoc />
    public SortDirection DefaultDirection { get; set; }

    /// <inheritdoc />
    public bool AllowsReverse { get; set; }

    /// <summary>Constructs a rule via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepOrderSortRule Create(string name, string localizedName, string sortField, SortDirection defaultDirection, bool allowsReverse)
    {
        var result = AbstractTypeFactory<SalesRepOrderSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.SortField = sortField;
        result.DefaultDirection = defaultDirection;
        result.AllowsReverse = allowsReverse;
        return result;
    }
}
