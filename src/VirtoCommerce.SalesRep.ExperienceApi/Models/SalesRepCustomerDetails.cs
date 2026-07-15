using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Detailed information about a single customer organization the current Sales Rep is
/// responsible for, shown in the "Customer information" card of the customer profile (VCST-5308).
/// </summary>
public class SalesRepCustomerDetails
{
    /// <summary>Organization (member) id of the customer.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Organization (customer) name.</summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Primary contact of the organization: its owner, or the first contact member when no owner is set.
    /// </summary>
    public SalesRepContact PrimaryContact { get; set; }

    /// <summary>Primary phone number (the primary contact's, falling back to the organization's).</summary>
    public string Phone { get; set; }

    /// <summary>Account type — the organization's business category.</summary>
    public string AccountType { get; set; }

    /// <summary>Default ship-to location, formatted as "City, Region".</summary>
    public string ShipTo { get; set; }

    /// <summary>
    /// Projects a customer <see cref="Organization"/> (with its already-resolved <paramref name="primaryContact"/>)
    /// onto the detailed Sales Rep customer card.
    /// </summary>
    public static SalesRepCustomerDetails FromOrganization(Organization organization, Contact primaryContact)
    {
        var result = AbstractTypeFactory<SalesRepCustomerDetails>.TryCreateInstance();
        result.MapFrom(organization, primaryContact);
        return result;
    }

    /// <summary>
    /// Populates this instance from <paramref name="organization"/> and its <paramref name="primaryContact"/>.
    /// Override in a derived type (registered via <c>AbstractTypeFactory.OverrideType</c>) to map additional fields.
    /// </summary>
    protected virtual void MapFrom(Organization organization, Contact primaryContact)
    {
        OrganizationId = organization.Id;
        OrganizationName = organization.Name;
        AccountType = organization.BusinessCategory;
        ShipTo = FormatShipTo(organization);

        if (primaryContact != null)
        {
            PrimaryContact = SalesRepContact.FromContact(primaryContact);
        }

        // Phone: the primary contact's first, falling back to the organization's.
        Phone = primaryContact?.Phones?.FirstOrDefault() ?? organization.Phones?.FirstOrDefault();
    }

    private static string FormatShipTo(Organization organization)
    {
        var address = organization.Addresses?.FirstOrDefault(x => x.IsDefault)
            ?? organization.Addresses?.FirstOrDefault();

        if (address == null)
        {
            return null;
        }

        var hasCity = !string.IsNullOrWhiteSpace(address.City);
        var hasRegion = !string.IsNullOrWhiteSpace(address.RegionName);

        if (hasCity && hasRegion)
        {
            return $"{address.City}, {address.RegionName}";
        }

        if (hasCity)
        {
            return address.City;
        }

        return hasRegion ? address.RegionName : null;
    }
}
