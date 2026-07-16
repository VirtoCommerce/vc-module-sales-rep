using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

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

    /// <summary>URL of the organization's icon (set from the admin "Manage icon" blade).</summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// The organization's default address (or its first). Null when the organization has no address or the caller
    /// didn't select <c>address</c> (the load is field-driven). The storefront formats it for display.
    /// </summary>
    public CoreAddress Address { get; set; }

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
        IconUrl = organization.IconUrl;
        Address = organization.GetDefaultAddress();

        if (primaryContact != null)
        {
            PrimaryContact = SalesRepContact.FromContact(primaryContact);
        }

        // Phone: the primary contact's first, falling back to the organization's.
        Phone = primaryContact?.Phones?.FirstOrDefault() ?? organization.Phones?.FirstOrDefault();
    }
}
