using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerOrderStatisticsPeriod
{
    public decimal Total { get; set; }

    public int Count { get; set; }

    public decimal Average { get; set; }

    public DateTime? LastOrderDate { get; set; }

    public DateTime? FirstOrderDate { get; set; }

    public string CurrencyCode { get; set; }

    // Non-null when some orders were left out of the figures above because their currency is not configured
    // (and so could not be converted to CurrencyCode); the totals/counts are then partial. Null = complete.
    public string Warning { get; set; }
}
