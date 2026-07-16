using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Aggregated cart/project statistics for a single date range, with every monetary value already converted to one
/// currency. The primary widget metric is <see cref="Count"/> (e.g. "Active Projects"); totals are included for
/// symmetry with order statistics.
/// </summary>
public class CustomerCartStatisticsPeriod
{
    /// <summary>Sum of cart totals in the range, in the requested currency.</summary>
    public decimal Total { get; set; }

    /// <summary>Number of carts in the range.</summary>
    public int Count { get; set; }

    /// <summary>Average cart value in the range (<see cref="Total"/> / <see cref="Count"/>). Zero when there are none.</summary>
    public decimal Average { get; set; }

    /// <summary>Created date of the most recent cart in the range, or null when there are none.</summary>
    public DateTime? LastCartDate { get; set; }

    /// <summary>Currency code the monetary values (<see cref="Total"/>, <see cref="Average"/>) are expressed in.</summary>
    public string CurrencyCode { get; set; }
}
