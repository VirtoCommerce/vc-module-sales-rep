using System;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>Raw per-currency aggregate (sum + count + earliest/latest date) read from a module's EF store before conversion.</summary>
internal sealed class CurrencyStatisticAggregate
{
    public string Currency { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
    public DateTime? EarliestDate { get; set; }
    public DateTime? LatestDate { get; set; }
}
