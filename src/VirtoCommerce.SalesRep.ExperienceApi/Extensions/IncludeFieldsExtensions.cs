using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class IncludeFieldsExtensions
{
    public static bool IncludesField(this IList<string> includeFields, string fieldName)
    {
        return (includeFields ?? []).Any(path => !string.IsNullOrEmpty(path)
            && path.Split('.').Any(segment => segment.Equals(fieldName, StringComparison.OrdinalIgnoreCase)));
    }

    // `phone` falls back to the primary contact's number, so it needs the contact as much as `primaryContact` does.
    public static bool IncludesPrimaryContact(this IList<string> includeFields)
    {
        return includeFields.IncludesField(nameof(SalesRepCustomerDetails.PrimaryContact))
            || includeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone));
    }

    public static string GetPrimaryContactResponseGroup(this IList<string> includeFields)
    {
        var result = MemberResponseGroup.WithPhones;

        if (includeFields.IncludesChildField(nameof(SalesRepCustomerDetails.PrimaryContact), nameof(SalesRepContact.Emails)))
        {
            result |= MemberResponseGroup.WithEmails;
        }

        return result.ToString();
    }

    private static bool IncludesChildField(this IList<string> includeFields, string parentName, string fieldName)
    {
        return (includeFields ?? []).Any(path =>
        {
            var segments = (path ?? string.Empty).Split('.');
            return segments.Zip(segments.Skip(1)).Any(pair =>
                pair.First.Equals(parentName, StringComparison.OrdinalIgnoreCase)
                && pair.Second.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        });
    }
}
