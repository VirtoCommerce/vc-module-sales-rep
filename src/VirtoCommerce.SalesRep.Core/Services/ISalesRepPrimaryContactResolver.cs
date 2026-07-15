using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Resolves an organization's primary contact — its <c>OwnerId</c> contact, falling back to the oldest contact
/// member. Single-sources this rule so every consumer (the <c>salesRepCustomer</c> detail card, the
/// primary-contact recipient policy, …) agrees on "who is the primary contact" and cannot drift.
/// </summary>
public interface ISalesRepPrimaryContactResolver
{
    /// <summary>
    /// The primary <see cref="Contact"/> of <paramref name="organization"/>, or <c>null</c> when the organization
    /// has neither a resolvable owner nor any contact member. <paramref name="responseGroup"/> controls how richly
    /// the returned contact is loaded (e.g. emails and/or phones), since callers need different projections.
    /// </summary>
    Task<Contact> ResolvePrimaryContactAsync(Organization organization, string responseGroup = null);
}
