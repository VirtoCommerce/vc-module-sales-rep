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

    public IList<string> Emails { get; set; } = [];

    public IList<string> Phones { get; set; } = [];

    /// <summary>Projects a customer <see cref="Contact"/> onto the lightweight Sales Rep contact DTO.</summary>
    public static SalesRepContact FromContact(Contact contact)
    {
        var result = AbstractTypeFactory<SalesRepContact>.TryCreateInstance();
        result.MapFrom(contact);
        return result;
    }

    /// <summary>
    /// Populates this instance from <paramref name="contact"/>. Override in a derived type
    /// (registered via <c>AbstractTypeFactory.OverrideType</c>) to map additional fields.
    /// </summary>
    protected virtual void MapFrom(Contact contact)
    {
        Id = contact.Id;
        FirstName = contact.FirstName;
        LastName = contact.LastName;
        MiddleName = contact.MiddleName;
        FullName = contact.FullName;
        Name = contact.Name;
        About = contact.About;
        PhotoUrl = contact.PhotoUrl;
        Emails = contact.Emails ?? [];
        Phones = contact.Phones ?? [];
    }
}
