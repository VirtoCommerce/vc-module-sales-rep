using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using CoreAddress = VirtoCommerce.CoreModule.Core.Common.Address;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class MemberExtensions
{
    /// <summary>
    /// The member's default address — the one flagged <c>IsDefault</c>, or the first address as a fallback.
    /// Returns <c>null</c> when the member has no addresses (or they weren't loaded — the customer queries load
    /// them only when <c>address</c> is selected). Single-sources "which address" so the customer list and details
    /// projections can't drift on it.
    /// </summary>
    public static CoreAddress GetDefaultAddress(this Member member)
    {
        return member.Addresses?.FirstOrDefault(x => x.IsDefault) ?? member.Addresses?.FirstOrDefault();
    }
}
