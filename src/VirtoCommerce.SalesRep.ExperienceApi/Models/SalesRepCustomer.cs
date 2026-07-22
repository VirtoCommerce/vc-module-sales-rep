using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomer
{
    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public string AccountId { get; set; }

    public string AccountType { get; set; }

    public string IconUrl { get; set; }

    public CoreAddress Address { get; set; }

    public string StoreId { get; set; }

    public string CurrencyCode { get; set; }

    public static SalesRepCustomer FromOrganization(Member organization, string storeId, string currencyCode = null)
    {
        var result = AbstractTypeFactory<SalesRepCustomer>.TryCreateInstance();
        result.MapFrom(organization, storeId);
        result.CurrencyCode = currencyCode;
        return result;
    }

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
