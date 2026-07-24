using System.Collections.Generic;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepContact
{
    public string Id { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string MiddleName { get; set; }

    public string FullName { get; set; }

    public string Name { get; set; }

    public string About { get; set; }

    public string PhotoUrl { get; set; }

    public IList<string> Emails { get; set; } = [];

    public IList<string> Phones { get; set; } = [];

    public static SalesRepContact FromContact(Contact contact)
    {
        var result = AbstractTypeFactory<SalesRepContact>.TryCreateInstance();
        result.MapFrom(contact);
        return result;
    }

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
