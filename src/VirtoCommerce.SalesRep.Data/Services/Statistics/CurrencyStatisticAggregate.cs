using System;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

internal sealed class CurrencyStatisticAggregate
{
    public string Currency { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
    public DateTime? EarliestDate { get; set; }
    public DateTime? LatestDate { get; set; }
}
