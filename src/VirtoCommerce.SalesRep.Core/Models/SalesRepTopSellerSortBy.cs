namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>The metric the Top Sellers ranking sorts by.</summary>
public enum SalesRepTopSellerSortBy
{
    /// <summary>Sum of quantities (units sold).</summary>
    Units,

    /// <summary>Sum of quantity × unit price (converted to one currency).</summary>
    Revenue,
}
