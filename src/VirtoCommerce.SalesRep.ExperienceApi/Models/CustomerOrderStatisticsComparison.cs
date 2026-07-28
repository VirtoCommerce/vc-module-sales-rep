namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class CustomerOrderStatisticsComparison
{
    public decimal TotalChange { get; set; }

    public decimal? TotalChangePercent { get; set; }

    public int CountChange { get; set; }

    public decimal? CountChangePercent { get; set; }

    public decimal AverageChange { get; set; }

    public decimal? AverageChangePercent { get; set; }

    public string CurrencyCode { get; set; }
}
