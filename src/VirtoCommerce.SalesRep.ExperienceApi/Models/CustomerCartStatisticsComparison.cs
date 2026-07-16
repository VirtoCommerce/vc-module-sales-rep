namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Period-over-period comparison (current vs previous) for cart/project statistics, computed server-side from two
/// <see cref="VirtoCommerce.SalesRep.Core.Models.CustomerCartStatisticsPeriod"/> buckets. Each metric carries both
/// the absolute change and the percentage change; percentages are null when the previous value is zero.
/// </summary>
public class CustomerCartStatisticsComparison
{
    /// <summary>Current total minus previous total, in the requested currency.</summary>
    public decimal TotalChange { get; set; }

    /// <summary>Percentage change of total; null when the previous total is zero.</summary>
    public decimal? TotalChangePercent { get; set; }

    /// <summary>Current count minus previous count.</summary>
    public int CountChange { get; set; }

    /// <summary>Percentage change of count; null when the previous count is zero.</summary>
    public decimal? CountChangePercent { get; set; }

    /// <summary>Current average minus previous average, in the requested currency.</summary>
    public decimal AverageChange { get; set; }

    /// <summary>Percentage change of average; null when the previous average is zero.</summary>
    public decimal? AverageChangePercent { get; set; }

    /// <summary>Currency the monetary change values (<see cref="TotalChange"/>, <see cref="AverageChange"/>) are in.</summary>
    public string CurrencyCode { get; set; }
}
