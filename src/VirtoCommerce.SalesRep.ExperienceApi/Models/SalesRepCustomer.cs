namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A customer organization the current Sales Rep is responsible for (VCST-5304).
/// </summary>
public class SalesRepCustomer
{
    /// <summary>Organization (member) id of the customer.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Organization (customer) name.</summary>
    public string OrganizationName { get; set; }
}
