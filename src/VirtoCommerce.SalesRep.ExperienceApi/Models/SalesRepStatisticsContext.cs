namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Shared backing object for the sales-rep statistics fields (orders, carts): the parameters common to every
/// <c>period</c>/<c>comparison</c> sub-field — the organizations aggregated, the calling rep's account id (creator
/// scope), the store scope, and the target currency. Date ranges (and cart kinds) come from the sub-field arguments,
/// not here. Each domain keeps its own concrete subclass so its GraphQL type stays distinct.
/// </summary>
public abstract class SalesRepStatisticsContext
{
    /// <summary>Organizations (customers) whose records are aggregated — one requested customer, or all the rep serves.</summary>
    public string[] OrganizationIds { get; set; }

    /// <summary>The calling sales rep's security-account id; statistics count only records they created.</summary>
    public string SalesRepUserId { get; set; }

    /// <summary>Store the records are scoped to (null = all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency all figures are converted to.</summary>
    public string CurrencyCode { get; set; }
}
