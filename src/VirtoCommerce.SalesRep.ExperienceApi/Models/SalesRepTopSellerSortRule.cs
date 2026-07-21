using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable ordering for the Sales Rep "Top Sellers" list (VCST-5309): "by-units" (default) and "by-revenue".
/// A <see cref="INamedSortRule"/>: the client sends the <see cref="Name"/>, the server maps it to the ranking
/// <see cref="SortBy"/> metric (via <c>ISalesRepTopSellerSortRuleResolver</c>). Extensible via a replacement resolver.
/// </summary>
public class SalesRepTopSellerSortRule : INamedSortRule
{
    /// <summary>Stable sort-rule id — the value the client sends back as the <c>salesRepTopSellers</c> "sort" argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the ordering.</summary>
    public string LocalizedName { get; set; }

    /// <summary>The ranking metric this rule maps to.</summary>
    public SalesRepTopSellerSortBy SortBy { get; set; }

    /// <inheritdoc />
    public SortDirection DefaultDirection { get; set; }

    /// <inheritdoc />
    public bool SupportsDirection { get; set; }

    /// <summary>Constructs a rule via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepTopSellerSortRule Create(string name, string localizedName, SalesRepTopSellerSortBy sortBy, SortDirection defaultDirection, bool supportsDirection)
    {
        var result = AbstractTypeFactory<SalesRepTopSellerSortRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.SortBy = sortBy;
        result.DefaultDirection = defaultDirection;
        result.SupportsDirection = supportsDirection;
        return result;
    }
}
