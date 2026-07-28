namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public abstract class SalesRepMonetaryStatisticsContext : SalesRepStatisticsContext
{
    public string CurrencyCode { get; set; }
}
