namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCountry
{
    /// <summary>ISO 3166-1 alpha-3 code — the value stored in <c>Address.CountryCode</c> and resolved by the platform.</summary>
    public string Id { get; set; }
    public string Name { get; set; }
}
