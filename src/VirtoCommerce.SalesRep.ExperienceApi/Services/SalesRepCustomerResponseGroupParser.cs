using System.Collections.Generic;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCustomerResponseGroupParser : ISalesRepCustomerResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        // organizationId/organizationName/iconUrl/accountType are scalar columns loaded with Default; only the
        // collection-backed fields opt into a heavier group.
        var result = MemberResponseGroup.Default;

        // address needs the member's Addresses collection loaded.
        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Address)))
        {
            result |= MemberResponseGroup.WithAddresses;
        }

        // phone falls back to the organization's own phones, so it needs the Phones collection loaded (the primary
        // contact's phones are loaded separately with the contact).
        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone)))
        {
            result |= MemberResponseGroup.WithPhones;
        }

        return result.ToString();
    }
}
