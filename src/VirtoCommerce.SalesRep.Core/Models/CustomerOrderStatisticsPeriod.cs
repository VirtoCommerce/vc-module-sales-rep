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
}
