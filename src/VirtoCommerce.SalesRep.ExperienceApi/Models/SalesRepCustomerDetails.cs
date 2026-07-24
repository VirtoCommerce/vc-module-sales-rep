using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerDetails
{
    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public SalesRepContact PrimaryContact { get; set; }

    public string Phone { get; set; }

    public string AccountType { get; set; }

    public string IconUrl { get; set; }

    public CoreAddress Address { get; set; }

    public static SalesRepCustomerDetails FromOrganization(Organization organization, Contact primaryContact)
    {
        var result = AbstractTypeFactory<SalesRepCustomerDetails>.TryCreateInstance();
        result.MapFrom(organization, primaryContact);
        return result;
    }

    protected virtual void MapFrom(Organization organization, Contact primaryContact)
    {
        OrganizationId = organization.Id;
        OrganizationName = organization.Name;
        AccountType = organization.BusinessCategory;
        IconUrl = organization.IconUrl;
        Address = organization.GetDefaultAddress();

        if (primaryContact != null)
        {
            PrimaryContact = SalesRepContact.FromContact(primaryContact);
        }

        Phone = primaryContact?.Phones?.FirstOrDefault() ?? organization.Phones?.FirstOrDefault();
    }
}
