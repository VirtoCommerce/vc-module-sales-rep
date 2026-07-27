using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerCartStatisticsPeriod
{
    public decimal Total { get; set; }

    public int Count { get; set; }

    public decimal Average { get; set; }

    public DateTime? LastCartDate { get; set; }

    public string CurrencyCode { get; set; }

    // Non-null when some carts were left out of the figures above because their currency is not configured
    // (and so could not be converted to CurrencyCode); the totals/counts are then partial. Null = complete.
    public string Warning { get; set; }
}
