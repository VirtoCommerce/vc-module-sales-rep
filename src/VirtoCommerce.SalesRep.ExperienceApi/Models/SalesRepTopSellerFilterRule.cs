using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable category badge for the Sales Rep "Top Sellers" list (VCST-5309). Default = each top-level non-hidden
/// category of the store's catalog, 1:1 (the <see cref="Name"/> is the category id). A <see cref="INamedFilterRule"/>:
/// the client sends the <see cref="Name"/>, the server expands it to the category's subtree and restricts the ranking
/// to line items in that subtree (via <c>ISalesRepTopSellerFilterRuleResolver</c>). Extensible: a project registers
/// its own resolver to group categories or add custom rules.
/// </summary>
public class SalesRepTopSellerFilterRule : INamedFilterRule
{
    /// <summary>Stable rule id — the top-level category id; the value the client sends back in the <c>filter</c> argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the category.</summary>
    public string LocalizedName { get; set; }

    /// <summary>Constructs a rule via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepTopSellerFilterRule Create(string name, string localizedName)
    {
        var result = AbstractTypeFactory<SalesRepTopSellerFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        return result;
    }
}
