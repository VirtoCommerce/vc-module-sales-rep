using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Contact information for a Sales Rep serving the caller's organization (VCST-4907),
/// so an organization member can reach out to them.
/// </summary>
public class SalesRepContact
{
    /// <summary>Contact (member) id of the Sales Rep.</summary>
    public string Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string MiddleName { get; set; }

    public string FullName { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    public string About { get; set; }

    public string PhotoUrl { get; set; }

    public IList<string> Emails { get; set; } = new List<string>();

    public IList<string> Phones { get; set; } = new List<string>();
}
