using System.Collections.Generic;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepMemberResponseGroupParser : ISalesRepMemberResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        // Scalar projections (id/name/iconUrl/accountType/photoUrl/…) load with Default; only collection-backed
        // fields opt into a heavier group. Field names are matched across the module's member projections
        // (SalesRepCustomer/Details and SalesRepContact) — each DTO exposes only its own subset, and IncludesField
        // matches whole path segments, so a field name never cross-fires (e.g. "phone" never matches "phones").
        var result = MemberResponseGroup.Default;

        // address (SalesRepCustomer/Details) → the Addresses collection.
        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Address)))
        {
            result |= MemberResponseGroup.WithAddresses;
        }

        // phone (SalesRepCustomerDetails; falls back to the org's own phones) or phones (SalesRepContact) → Phones.
        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone))
            || includeFields.IncludesField(nameof(SalesRepContact.Phones)))
        {
            result |= MemberResponseGroup.WithPhones;
        }

        // emails (SalesRepContact) → the Emails collection.
        if (includeFields.IncludesField(nameof(SalesRepContact.Emails)))
        {
            result |= MemberResponseGroup.WithEmails;
        }

        return result.ToString();
    }
}
