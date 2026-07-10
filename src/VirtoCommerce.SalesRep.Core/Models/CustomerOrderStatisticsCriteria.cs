using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Criteria for a single order-statistics bucket: all orders of one organization (optionally scoped to a store
/// and a set of statuses) whose created date falls in [<see cref="FromDate"/>, <see cref="ToDate"/>), aggregated
/// and converted to <see cref="CurrencyCode"/>. Cancelled and prototype orders are always excluded.
/// </summary>
public class CustomerOrderStatisticsCriteria
{
    /// <summary>
    /// Organization (customer) whose orders are aggregated. Scoping to a single organization is the caller's
    /// responsibility — that the caller may see this organization is enforced upstream (in the query handler).
    /// </summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the orders to. Null aggregates across all stores.</summary>
    public string StoreId { get; set; }

    /// <summary>Currency all monetary figures are converted to (order amounts are stored per order currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Optional order-status whitelist. Null/empty counts every non-cancelled status.</summary>
    public string[] Statuses { get; set; }

    /// <summary>Inclusive lower bound on the order created date. Null = no lower bound (e.g. lifetime).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Exclusive upper bound on the order created date. Null = no upper bound.</summary>
    public DateTime? ToDate { get; set; }
}
