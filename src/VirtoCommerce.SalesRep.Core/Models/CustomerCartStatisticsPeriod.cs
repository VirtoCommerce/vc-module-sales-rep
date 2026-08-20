namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerCartStatisticsPeriod
{
    public int SelectedItemQuantity { get; set; }

    public int UnselectedItemQuantity { get; set; }

    public int Count { get; set; }

    public decimal Total { get; set; }

    public decimal Average { get; set; }

    public string CurrencyCode { get; set; }

    public string Warning { get; set; }
}
