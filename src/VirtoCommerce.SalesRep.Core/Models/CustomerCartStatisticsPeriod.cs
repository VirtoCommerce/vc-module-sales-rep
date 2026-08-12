namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerCartStatisticsPeriod
{
    public int SelectedItemQuantity { get; set; }

    public int UnselectedItemQuantity { get; set; }

    /// <summary>
    /// Number of distinct carts contributing to <see cref="Total"/> — those holding at least one line picked for
    /// checkout in the range, gifts excluded. Zero unless the caller selected a cart figure
    /// (see <see cref="CustomerCartStatisticsCriteria.IncludeCartFigures"/>).
    /// </summary>
    public int Count { get; set; }

    public decimal Total { get; set; }

    public decimal Average { get; set; }

    public string CurrencyCode { get; set; }

    public string Warning { get; set; }
}
