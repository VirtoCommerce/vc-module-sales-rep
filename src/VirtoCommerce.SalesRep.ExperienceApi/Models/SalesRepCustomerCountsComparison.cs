namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Period-over-period comparison (current vs previous) for the "my customers" counters, computed server-side from
/// two <see cref="VirtoCommerce.SalesRep.Core.Models.SalesRepCustomerCountsPeriod"/> buckets. Percentages are null
/// when the previous value is zero.
/// </summary>
public class SalesRepCustomerCountsComparison
{
    /// <summary>Current ordering-customers count minus previous.</summary>
    public int OrderingCustomersChange { get; set; }

    /// <summary>Percentage change of ordering-customers; null when the previous count is zero.</summary>
    public decimal? OrderingCustomersChangePercent { get; set; }

    /// <summary>Current new-customers count minus previous.</summary>
    public int NewCustomersChange { get; set; }

    /// <summary>Percentage change of new-customers; null when the previous count is zero.</summary>
    public decimal? NewCustomersChangePercent { get; set; }
}
