using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepPrimaryContactResolver
{
    Task<Contact> ResolvePrimaryContactAsync(Organization organization, string responseGroup);

    /// <summary>
    /// Resolves the primary contact of each organization, keyed by organization id. Organizations without one are left out.
    /// </summary>
    async Task<IDictionary<string, Contact>> ResolvePrimaryContactsAsync(IList<Organization> organizations, string responseGroup)
    {
        var result = new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);

        foreach (var organization in organizations)
        {
            var primaryContact = await ResolvePrimaryContactAsync(organization, responseGroup);
            if (primaryContact != null)
            {
                result[organization.Id] = primaryContact;
            }
        }

        return result;
    }
}
