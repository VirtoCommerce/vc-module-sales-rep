using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Resolved ordering for the "My customers" list. Two shapes: a plain member-column sort (<see cref="MemberSort"/>,
/// applied straight to the members search), or an order-derived ordering (<see cref="IsOrderDerived"/>) the members
/// search can't express — the handler then ranks the served organizations by the rep's per-organization order
/// aggregate (<see cref="Metric"/> over the optional [<see cref="FromDate"/>, <see cref="ToDate"/>) window, biggest/
/// newest first) before paging.
/// </summary>
public class SalesRepCustomerSortSpec
{
    /// <summary>True when the ordering is computed from the rep's orders (ranked in the handler), not a member column.</summary>
    public bool IsOrderDerived { get; set; }

    /// <summary>Members-search sort expression when not order-derived (e.g. "name:asc").</summary>
    public string MemberSort { get; set; }

    /// <summary>Which per-organization order aggregate to rank by, when order-derived.</summary>
    public SalesRepCustomerSortMetric Metric { get; set; }

    /// <summary>Inclusive lower bound for the order-derived metric's window (null = all time).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Exclusive upper bound for the order-derived metric's window (null = up to now).</summary>
    public DateTime? ToDate { get; set; }
}

/// <summary>The per-organization order aggregate an order-derived customer sort ranks by.</summary>
public enum SalesRepCustomerSortMetric
{
    /// <summary>Most recent order's created date.</summary>
    LastOrderDate,

    /// <summary>Sum of order totals in the window (converted to one currency).</summary>
    Total,
}
