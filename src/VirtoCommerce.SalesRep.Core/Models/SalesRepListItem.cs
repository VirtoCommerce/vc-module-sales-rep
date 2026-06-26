using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Lightweight Sales Rep row for the list/grid.
/// </summary>
public class SalesRepListItem
{
    /// <summary>Contact member id.</summary>
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }

    /// <summary>Number of organizations the rep serves.</summary>
    public int OrganizationsCount { get; set; }

    public bool IsLocked { get; set; }
    public bool HasGlobalSalesRepRole { get; set; }

    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
