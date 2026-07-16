namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the cart-statistics field: the parameters shared by every <c>period</c>/<c>comparison</c>
/// sub-field (organizations, store, target currency). Date ranges and cart kinds come from the sub-field arguments.
/// </summary>
public class CustomerCartStatisticsContext
{
    /// <summary>Organizations (customers) whose carts are aggregated — one requested customer, or all the rep serves.</summary>
    public string[] OrganizationIds { get; set; }

    /// <summary>The calling sales rep's security-account id; statistics count only carts they created.</summary>
    public string SalesRepUserId { get; set; }

    /// <summary>Store the carts are scoped to (null = all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency all figures are converted to.</summary>
    public string CurrencyCode { get; set; }
}
