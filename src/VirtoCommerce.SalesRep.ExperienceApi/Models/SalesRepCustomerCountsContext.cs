namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the "my customers" counts field: the parameters shared by <c>assignedCustomers</c> and every
/// <c>period</c>/<c>comparison</c> sub-field. Date ranges come from the sub-field arguments.
/// </summary>
public class SalesRepCustomerCountsContext
{
    /// <summary>
    /// Organizations the rep serves (or the one requested customer). <c>assignedCustomers</c> is this set's size;
    /// the period counters are computed over the rep's orders within it.
    /// </summary>
    public string[] OrganizationIds { get; set; }

    /// <summary>The calling sales rep's security-account id; counts consider only orders they created.</summary>
    public string SalesRepUserId { get; set; }

    /// <summary>Store the orders are scoped to (null = all stores).</summary>
    public string StoreId { get; set; }
}
