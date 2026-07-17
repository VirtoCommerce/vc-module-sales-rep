namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// Shared constants for the Sales Rep named-filter-rule selection. One argument name across every query that filters
/// by a named rule (the orders/customers lists, the order/cart statistics, the customer counts), so the storefront
/// learns a single convention regardless of domain — the value is a single, optional rule <c>name</c> from the
/// matching discovery query (<c>salesRepOrderFilterRules</c>, <c>salesRepCartFilterRules</c>,
/// <c>salesRepCustomerFilterRules</c>). Omitting it selects the baseline (security-scoped, non-deleted) set.
/// </summary>
public static class SalesRepFilters
{
    /// <summary>The unified GraphQL argument name for selecting a single named filter rule (optional).</summary>
    public const string ArgumentName = "filter";
}
