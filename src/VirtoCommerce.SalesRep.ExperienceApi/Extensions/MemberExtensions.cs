using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class MemberExtensions
{
    public static CoreAddress GetDefaultAddress(this Member member)
    {
        return member.Addresses?.FirstOrDefault(x => x.IsDefault) ?? member.Addresses?.FirstOrDefault();
    }

    public static string GetPrimaryPhone(this Member organization, Contact primaryContact)
    {
        return primaryContact?.Phones?.FirstOrDefault() ?? organization.Phones?.FirstOrDefault();
    }
}
