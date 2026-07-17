using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A selectable customer segment for the Sales Rep customer surfaces — shared by the customers list and the
/// "my customers" counts. A <see cref="INamedFilterRule"/>: the client sends the <see cref="Name"/>, the server
/// resolves it and narrows the customer/order query (via <c>ISalesRepCustomerFilterRuleResolver</c>). Unlike order
/// statuses / cart kinds, a customer segment ("active", "at-risk", …) is a behavioral predicate the module leaves
/// undefined by default; projects register their own resolver (and, for a predicate the standard criteria can't
/// express, subclass the reader) to add segments. Extensible via <c>AbstractTypeFactory.OverrideType</c>.
/// </summary>
public class SalesRepCustomerFilterRule : INamedFilterRule
{
    /// <summary>Stable segment id — the value the client sends back in the <c>filter</c> argument.</summary>
    public string Name { get; set; }

    /// <summary>Localized label shown for the segment.</summary>
    public string LocalizedName { get; set; }

    /// <summary>Constructs a segment via <see cref="AbstractTypeFactory{T}"/> so downstream can override the type.</summary>
    public static SalesRepCustomerFilterRule Create(string name, string localizedName)
    {
        var result = AbstractTypeFactory<SalesRepCustomerFilterRule>.TryCreateInstance();
        result.Name = name;
        result.LocalizedName = localizedName;
        return result;
    }
}
