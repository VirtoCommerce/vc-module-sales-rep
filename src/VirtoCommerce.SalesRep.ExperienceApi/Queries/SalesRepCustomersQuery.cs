using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the customer organizations the current Sales Rep is responsible for (VCST-5304).
/// The Sales Rep is the caller; their security account id is set server-side from the caller's claims.
/// </summary>
public class SalesRepCustomersQuery : SearchQuery<SalesRepCustomerSearchResult>
{
    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }
}
