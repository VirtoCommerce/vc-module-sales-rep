namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Detailed information about a single customer organization the current Sales Rep is
/// responsible for, shown in the "Customer information" card of the customer profile (VCST-5308).
/// </summary>
public class SalesRepCustomerDetails
{
    /// <summary>Organization (member) id of the customer.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Organization (customer) name.</summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Primary contact of the organization: its owner, or the first contact member when no owner is set.
    /// </summary>
    public SalesRepContact PrimaryContact { get; set; }

    /// <summary>Primary phone number (the primary contact's, falling back to the organization's).</summary>
    public string Phone { get; set; }

    /// <summary>Account type — the organization's business category.</summary>
    public string AccountType { get; set; }

    /// <summary>Default ship-to location, formatted as "City, Region".</summary>
    public string ShipTo { get; set; }
}
