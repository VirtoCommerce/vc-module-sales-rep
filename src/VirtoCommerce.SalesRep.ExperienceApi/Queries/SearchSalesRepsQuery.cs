using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the Sales Reps that support the caller's organization (VCST-4907).
/// The organization is taken from the caller's identity, not from arguments.
/// </summary>
public class SearchSalesRepsQuery : SearchQuery<SalesRepContactSearchResult>
{
    /// <summary>Organization the reps are resolved for (set server-side from the current user's claims).</summary>
    public string OrganizationId { get; set; }
}
