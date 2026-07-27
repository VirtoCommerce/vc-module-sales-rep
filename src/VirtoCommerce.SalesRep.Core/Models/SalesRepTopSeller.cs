namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepTopSeller
{
    public int Rank { get; set; }

    public string ProductId { get; set; }

    public string Name { get; set; }

    public string Sku { get; set; }

    public string ImageUrl { get; set; }

    public string CategoryId { get; set; }

    public int Units { get; set; }

    public decimal Revenue { get; set; }

    public string CurrencyCode { get; set; }

    // Non-null when some of this product's sales were left out of Revenue because their currency is not configured
    // (and so could not be converted to CurrencyCode); Revenue is then partial. Null = complete.
    public string Warning { get; set; }
}
