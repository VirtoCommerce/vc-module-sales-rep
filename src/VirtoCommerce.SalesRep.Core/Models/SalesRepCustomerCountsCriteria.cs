using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Criteria for the Sales Rep "my customers" counters over one date range. "Ordering customers" is derived from the
/// rep's own orders (creator-scoped) within their served organizations; "new customers" is derived from
/// <see cref="AssignmentDates"/> (when each served customer was assigned to the rep), independent of orders.
/// Cancelled and prototype orders are always excluded.
/// </summary>
public class SalesRepCustomerCountsCriteria
{
    /// <summary>
    /// Organizations (customers) the counts are computed over — every organization the rep serves (or one requested
    /// customer). Enforced upstream in the query handler; an empty/null set counts nothing.
    /// </summary>
    public string[] OrganizationIds { get; set; }

    /// <summary>
    /// The date each served organization was first assigned to the rep (one per organization). The "new customers"
    /// counter counts how many fall within [<see cref="FromDate"/>, <see cref="ToDate"/>]. Set by the query handler
    /// from the rep's granting memberships; null/empty means no new customers.
    /// </summary>
    public DateTime[] AssignmentDates { get; set; }

    /// <summary>
    /// Only orders created by this user are considered — the sales rep's own security-account id (the creator-scoping
    /// half of the data-isolation invariant). The query handler always sets it to the caller.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>Optional store to scope the orders to. Null counts across all stores.</summary>
    public string StoreId { get; set; }

    /// <summary>Inclusive lower bound on the order created date. Null = no lower bound.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound on the order created date. Null = no upper bound.</summary>
    public DateTime? ToDate { get; set; }
}
