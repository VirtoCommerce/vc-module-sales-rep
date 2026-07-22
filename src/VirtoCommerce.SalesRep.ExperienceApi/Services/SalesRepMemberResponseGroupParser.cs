using System.Collections.Generic;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepMemberResponseGroupParser : ISalesRepMemberResponseGroupParser
{
    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        var result = MemberResponseGroup.Default;

        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Address)))
        {
            result |= MemberResponseGroup.WithAddresses;
        }

        if (includeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone))
            || includeFields.IncludesField(nameof(SalesRepContact.Phones)))
        {
            result |= MemberResponseGroup.WithPhones;
        }

        if (includeFields.IncludesField(nameof(SalesRepContact.Emails)))
        {
            result |= MemberResponseGroup.WithEmails;
        }

        return result.ToString();
    }
}
