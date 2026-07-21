namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A <see cref="SalesRepStatisticsContext"/> for money-bearing statistics (orders, carts) — adds the target currency
/// all figures are converted to. The customer-counts context has no currency axis and extends the base directly.
/// </summary>
public abstract class SalesRepMonetaryStatisticsContext : SalesRepStatisticsContext
{
    /// <summary>Currency all figures are converted to.</summary>
    public string CurrencyCode { get; set; }
}
