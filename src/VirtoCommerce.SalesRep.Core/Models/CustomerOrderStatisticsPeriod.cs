using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Aggregated order statistics for a single date range, with every monetary value already converted to one currency.
/// </summary>
public class CustomerOrderStatisticsPeriod
{
    /// <summary>Inclusive lower bound of the range (null = unbounded).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Exclusive upper bound of the range (null = unbounded).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Sum of order totals in the range, in the requested currency.</summary>
    public decimal Total { get; set; }

    /// <summary>Number of orders in the range.</summary>
    public int Count { get; set; }

    /// <summary>
    /// Average order value in the range (<see cref="Total"/> / <see cref="Count"/>), in the requested currency.
    /// Zero when there are no orders.
    /// </summary>
    public decimal Average { get; set; }

    /// <summary>Created date of the most recent order in the range, or null when there are none.</summary>
    public DateTime? LastOrderDate { get; set; }
}
