using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCustomerResponseGroupParser : ISalesRepCustomerResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        var fields = includeFields ?? [];

        // Match on any path segment (not just the leaf) so an object-valued field like `address { city }` — whose
        // leaf is "city" — is still recognized by its "address" segment.
        bool Requested(string fieldName) =>
            fields.Any(path => !string.IsNullOrEmpty(path)
                && path.Split('.').Any(segment => segment.Equals(fieldName, StringComparison.OrdinalIgnoreCase)));

        // organizationId/organizationName/iconUrl/accountType are scalar columns loaded with Default; only the
        // collection-backed fields opt into a heavier group.
        var result = MemberResponseGroup.Default;

        // address needs the member's Addresses collection loaded.
        if (Requested(nameof(SalesRepCustomerDetails.Address)))
        {
            result |= MemberResponseGroup.WithAddresses;
        }

        // phone falls back to the organization's own phones, so it needs the Phones collection loaded (the primary
        // contact's phones are loaded separately with the contact).
        if (Requested(nameof(SalesRepCustomerDetails.Phone)))
        {
            result |= MemberResponseGroup.WithPhones;
        }

        return result.ToString();
    }
}
