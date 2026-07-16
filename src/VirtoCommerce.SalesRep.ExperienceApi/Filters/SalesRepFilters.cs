namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// Shared constants for the Sales Rep named-filter-rule selection. One argument name across every query that filters
/// by named rules (the orders list, the order statistics, the cart statistics), so the storefront learns a single
/// convention regardless of domain — the value is always a list of rule <c>name</c>s from the matching discovery
/// query (<c>salesRepOrderStatuses</c>, <c>salesRepCartKinds</c>, …).
/// </summary>
public static class SalesRepFilters
{
    /// <summary>The unified GraphQL argument name for selecting named filter rules.</summary>
    public const string ArgumentName = "filters";
}
