using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomer
{
    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public string IconUrl { get; set; }

    public CoreAddress Address { get; set; }

    public string StoreId { get; set; }

    public static SalesRepCustomer FromOrganization(Member organization, string storeId)
    {
        var result = AbstractTypeFactory<SalesRepCustomer>.TryCreateInstance();
        result.MapFrom(organization, storeId);
        return result;
    }

    protected virtual void MapFrom(Member organization, string storeId)
    {
        OrganizationId = organization.Id;
        OrganizationName = organization.Name;
        IconUrl = organization.IconUrl;
        Address = organization.GetDefaultAddress();
        StoreId = storeId;
    }
}
