using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A customer organization the current Sales Rep is responsible for (VCST-5304).
/// </summary>
public class SalesRepCustomer
{
    /// <summary>Organization (member) id of the customer.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Organization (customer) name.</summary>
    public string OrganizationName { get; set; }

    /// <summary>External/display account id (the organization's <c>OuterId</c>); null when it has none.</summary>
    public string AccountId { get; set; }

    /// <summary>Account type — the organization's business category (e.g. "Garden Center"). Null when not set.</summary>
    public string AccountType { get; set; }

    /// <summary>URL of the organization's icon (set from the admin "Manage icon" blade).</summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// The organization's default address (or its first). Null when the organization has no address or the caller
    /// didn't select <c>address</c> (the load is field-driven). The storefront formats it for display.
    /// </summary>
    public CoreAddress Address { get; set; }

    /// <summary>
    /// Store the caller is browsing (from the query's <c>storeId</c> argument). Not exposed as a GraphQL field —
    /// it scopes the <c>lastOrder</c> lookup and inline order statistics so a rep never sees another store's orders.
    /// Null = no store filter.
    /// </summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Currency the inline <c>orderStatistics</c> figures default to (resolved once by the handler — the platform
    /// primary currency). Not exposed as a GraphQL field; the field's own <c>currencyCode</c> argument overrides it.
    /// </summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Projects an organization <see cref="Member"/> onto the Sales Rep customer DTO, carrying the caller's
    /// <paramref name="storeId"/> (so <c>lastOrder</c>/<c>orderStatistics</c> scope to that store) and the default
    /// <paramref name="currencyCode"/> for the inline statistics.
    /// </summary>
    public static SalesRepCustomer FromOrganization(Member organization, string storeId, string currencyCode = null)
    {
        var result = AbstractTypeFactory<SalesRepCustomer>.TryCreateInstance();
        result.MapFrom(organization, storeId);
        result.CurrencyCode = currencyCode;
        return result;
    }

    /// <summary>
    /// Populates this instance from <paramref name="organization"/>. Override in a derived type
    /// (registered via <c>AbstractTypeFactory.OverrideType</c>) to map additional fields.
    /// </summary>
    protected virtual void MapFrom(Member organization, string storeId)
    {
        OrganizationId = organization.Id;
        OrganizationName = organization.Name;
        AccountId = organization.OuterId;
        AccountType = (organization as Organization)?.BusinessCategory;
        IconUrl = organization.IconUrl;
        Address = organization.GetDefaultAddress();
        StoreId = storeId;
    }
}
