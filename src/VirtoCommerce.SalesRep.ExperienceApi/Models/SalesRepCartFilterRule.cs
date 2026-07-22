using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable cart "kind" for the Sales Rep cart/project widgets — the cart analogue of
/// <see cref="SalesRepOrderFilterRule"/>, but richer: a kind is a composite filter over cart <see cref="Types"/> and/or
/// <see cref="Statuses"/> (e.g. the built-in "active-carts" → excludes type "Wishlist" and requires non-empty; a
/// project could add a "project" → type "Wishlist" kind). Extensible: a project registers its own <c>ISalesRepCartFilterRuleResolver</c> and/or overrides
/// this type via <c>AbstractTypeFactory.OverrideType</c> to add, hide or recompose kinds.
/// </summary>
public class SalesRepCartFilterRule : INamedFilterRule
{
    /// <summary>Stable kind id — the value the client sends back in the cart-statistics <c>filters</c> argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the kind.</summary>
    public string LocalizedName { get; set; }

    /// <summary>Underlying cart types this kind maps to (empty = any type).</summary>
    public IList<string> Types { get; set; } = [];

    /// <summary>Cart types this kind excludes (e.g. "Wishlist" to keep projects out of an "active carts" kind).</summary>
    public IList<string> ExcludeTypes { get; set; } = [];

    /// <summary>Underlying cart statuses this kind maps to (empty = any status).</summary>
    public IList<string> Statuses { get; set; } = [];

    /// <summary>When true, the kind counts only non-empty carts (carts with at least one line item).</summary>
    public bool OnlyNonEmpty { get; set; }

    /// <summary>Constructs a kind via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepCartFilterRule Create(
        string name,
        string localizedName,
        string[] types = null,
        string[] statuses = null,
        string[] excludeTypes = null,
        bool onlyNonEmpty = false)
    {
        var result = AbstractTypeFactory<SalesRepCartFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        result.Types = types ?? [];
        result.Statuses = statuses ?? [];
        result.ExcludeTypes = excludeTypes ?? [];
        result.OnlyNonEmpty = onlyNonEmpty;
        return result;
    }
}
