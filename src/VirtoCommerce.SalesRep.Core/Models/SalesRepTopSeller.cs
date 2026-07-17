namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// One ranked product in the Sales Rep "Top Sellers" list (VCST-5309, dashboard + customer). Aggregates the rep's
/// own sales of a product over a period from order line items — a self-contained snapshot: name / sku / image /
/// category are denormalized on the line item, so the row needs no catalog read. Monetary <see cref="Revenue"/> is
/// already converted to <see cref="CurrencyCode"/>.
/// </summary>
public class SalesRepTopSeller
{
    /// <summary>1-based rank within the returned list (by the selected metric).</summary>
    public int Rank { get; set; }

    /// <summary>Product id the sales were aggregated by (the purchased product/variation).</summary>
    public string ProductId { get; set; }

    /// <summary>Product name (from the line-item snapshot).</summary>
    public string Name { get; set; }

    /// <summary>Product SKU (from the line-item snapshot).</summary>
    public string Sku { get; set; }

    /// <summary>Product image URL (from the line-item snapshot); null when the line items carried none.</summary>
    public string ImageUrl { get; set; }

    /// <summary>Category id (from the line-item snapshot); the frontend resolves its name if needed.</summary>
    public string CategoryId { get; set; }

    /// <summary>Total units sold — sum of line-item quantities.</summary>
    public int Units { get; set; }

    /// <summary>Total revenue — sum of (quantity × unit price), converted to <see cref="CurrencyCode"/>.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Currency code the <see cref="Revenue"/> is expressed in.</summary>
    public string CurrencyCode { get; set; }
}
