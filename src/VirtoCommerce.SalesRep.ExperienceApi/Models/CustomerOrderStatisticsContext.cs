namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the <c>statistics</c> field: the parameters shared by every <c>period</c>/<c>comparison</c>
/// sub-field of one customer's statistics (organization, store, target currency). The date ranges come from the
/// sub-field arguments, not from here.
/// </summary>
public class CustomerOrderStatisticsContext
{
    /// <summary>Organization (customer) whose orders are aggregated.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Store the orders are scoped to (null = all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency all figures are converted to.</summary>
    public string CurrencyCode { get; set; }
}
