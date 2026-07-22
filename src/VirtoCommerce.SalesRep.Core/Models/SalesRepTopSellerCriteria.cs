using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Criteria for the Sales Rep "Top Sellers" ranking (VCST-5309): the rep's own order line items within the served
/// organizations (and an optional store / category subtree / created-date range), grouped by product, ranked by
/// <see cref="SortBy"/>, returning only the top <see cref="Take"/>. Cancelled/prototype orders and cancelled line
/// items are always excluded. Which organizations the caller may see is enforced upstream (the query handler).
/// </summary>
public class SalesRepTopSellerCriteria : ValueObject
{
    /// <summary>Organizations (customers) whose line items are aggregated. Empty/null aggregates nothing.</summary>
    public IList<string> OrganizationIds { get; set; }

    /// <summary>
    /// Creator scope (data-isolation invariant): only line items of orders created by this user — the rep's own
    /// security-account id (rep-created orders record it as the order's <c>CustomerId</c>).
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>Optional store to scope the orders to. Null aggregates across all stores.</summary>
    public string StoreId { get; set; }

    /// <summary>Currency the revenue figures are converted to.</summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Optional category subtree to restrict to — the line-item <c>CategoryId</c> must be in this set (the filter
    /// rule resolves a top-level category to its descendant ids). Null/empty = all categories.
    /// </summary>
    public IList<string> CategoryIds { get; set; }

    /// <summary>
    /// Optional product-id restriction (VCST-5309, category filter option (a)): the aggregation is limited to these
    /// products. Null = no restriction; empty = match nothing (no rows). The default filter rule resolver sets this —
    /// not <see cref="CategoryIds"/> — because it resolves the selected category to product ids via the catalog
    /// index, which is the only correct membership source for a virtual store catalog (line-item <c>CategoryId</c>
    /// snapshots the physical category, so it never matches a virtual store category).
    /// </summary>
    public IList<string> ProductIds { get; set; }

    /// <summary>Inclusive lower bound on the order created date. Null = no lower bound (lifetime).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound on the order created date. Null = no upper bound.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>The metric the ranking sorts by.</summary>
    public SalesRepTopSellerSortBy SortBy { get; set; }

    /// <summary>Default value for <see cref="Take"/> when the caller doesn't specify one.</summary>
    public const int DefaultTake = 5;

    /// <summary>Max rows to return — the ranking is top-N only.</summary>
    public int Take { get; set; } = DefaultTake;
}

/// <summary>The metric the Top Sellers ranking sorts by.</summary>
public enum SalesRepTopSellerSortBy
{
    /// <summary>Sum of quantities (units sold).</summary>
    Units,

    /// <summary>Sum of quantity × unit price (converted to one currency).</summary>
    Revenue,
}
