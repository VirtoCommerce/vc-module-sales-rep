using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Resolved ordering for the "My customers" list. Two shapes: a plain member-column sort (<see cref="MemberSortField"/>,
/// applied straight to the members search), or an order-derived ordering (<see cref="IsOrderDerived"/>) the members
/// search can't express — the handler then ranks the served organizations by the rep's per-organization order
/// aggregate (<see cref="Metric"/> over the optional [<see cref="FromDate"/>, <see cref="ToDate"/>] window) before
/// paging. <see cref="Direction"/> is the resolved direction (the rule's natural default, or the opposite when the
/// client sent a <c>:asc</c>/<c>:desc</c> suffix the rule allows) and applies to both shapes.
/// </summary>
public class SalesRepCustomerSortSpec
{
    /// <summary>True when the ordering is computed from the rep's orders (ranked in the handler), not a member column.</summary>
    public bool IsOrderDerived { get; set; }

    /// <summary>Members-search sort COLUMN when not order-derived (e.g. "name"); the handler appends the direction.</summary>
    public string MemberSortField { get; set; }

    /// <summary>Which per-organization order aggregate to rank by, when order-derived.</summary>
    public SalesRepCustomerSortMetric Metric { get; set; }

    /// <summary>Resolved sort direction (set by the resolver from the rule's default direction or an explicit <c>:asc</c>/<c>:desc</c> suffix).</summary>
    public SortDirection Direction { get; set; }

    /// <summary>Inclusive lower bound for the order-derived metric's window (null = all time).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound for the order-derived metric's window (null = up to now).</summary>
    public DateTime? ToDate { get; set; }
}
