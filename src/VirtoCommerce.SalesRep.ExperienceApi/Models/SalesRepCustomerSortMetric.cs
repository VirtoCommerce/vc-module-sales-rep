namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>The per-organization order aggregate an order-derived customer sort ranks by.</summary>
public enum SalesRepCustomerSortMetric
{
    /// <summary>Most recent order's created date.</summary>
    LastOrderDate,

    /// <summary>Sum of order totals in the window (converted to one currency).</summary>
    Total,
}
