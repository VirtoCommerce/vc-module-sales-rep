using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

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

    /// <summary>
    /// Store the caller is browsing (from the query's <c>storeId</c> argument). Not exposed as a GraphQL field —
    /// it scopes the <c>lastOrder</c> lookup so a rep never sees another store's orders. Null = no store filter.
    /// </summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Projects an organization <see cref="Member"/> onto the Sales Rep customer DTO, carrying the caller's
    /// <paramref name="storeId"/> so the <c>lastOrder</c> resolver can scope orders to that store.
    /// </summary>
    public static SalesRepCustomer FromOrganization(Member organization, string storeId)
    {
        var result = AbstractTypeFactory<SalesRepCustomer>.TryCreateInstance();
        result.MapFrom(organization, storeId);
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
        StoreId = storeId;
    }
}
