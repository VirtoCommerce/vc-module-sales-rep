namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// An organization a Sales Rep serves, backed by an OrganizationMembership carrying the sales-rep role.
/// </summary>
public class SalesRepOrganization
{
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }

    /// <summary>Underlying OrganizationMembership id (null when not yet persisted).</summary>
    public string MembershipId { get; set; }
}
