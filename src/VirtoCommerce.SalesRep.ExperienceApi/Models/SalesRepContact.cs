using System.Collections.Generic;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

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

    /// <summary>Projects a customer <see cref="Contact"/> onto the lightweight Sales Rep contact DTO.</summary>
    public static SalesRepContact FromContact(Contact contact)
    {
        var result = AbstractTypeFactory<SalesRepContact>.TryCreateInstance();
        result.Id = contact.Id;
        result.FirstName = contact.FirstName;
        result.LastName = contact.LastName;
        result.MiddleName = contact.MiddleName;
        result.FullName = contact.FullName;
        result.Name = contact.Name;
        result.About = contact.About;
        result.PhotoUrl = contact.PhotoUrl;
        result.Emails = contact.Emails ?? new List<string>();
        result.Phones = contact.Phones ?? new List<string>();
        return result;
    }
}
