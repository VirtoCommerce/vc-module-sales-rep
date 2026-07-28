namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerCountsComparison
{
    public int OrderingCustomersChange { get; set; }

    public decimal? OrderingCustomersChangePercent { get; set; }

    public int NewCustomersChange { get; set; }

    public decimal? NewCustomersChangePercent { get; set; }
}
