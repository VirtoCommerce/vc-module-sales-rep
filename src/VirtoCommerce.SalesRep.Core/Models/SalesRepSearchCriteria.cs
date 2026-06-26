using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepSearchCriteria : SearchCriteriaBase
{
    /// <summary>Restrict to reps serving this organization (per-org membership).</summary>
    public string OrganizationId { get; set; }

    /// <summary>Only reps whose account is blocked (locked).</summary>
    public bool OnlyBlocked { get; set; }

    /// <summary>Only reps not assigned to any organization (e.g. global-role rep with no membership).</summary>
    public bool OnlyUnassigned { get; set; }
}
