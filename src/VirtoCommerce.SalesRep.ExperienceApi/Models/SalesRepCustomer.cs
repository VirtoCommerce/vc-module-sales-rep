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

    /// <summary>
    /// Store the caller is browsing (from the query's <c>storeId</c> argument). Not exposed as a GraphQL field —
    /// it scopes the <c>lastOrder</c> lookup so a rep never sees another store's orders. Null = no store filter.
    /// </summary>
    public string StoreId { get; set; }
}
