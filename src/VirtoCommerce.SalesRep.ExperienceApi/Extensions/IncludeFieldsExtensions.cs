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
    // Only the customer's own fields count: `address { phone }` needs no contact.
    public static bool IncludesPrimaryContact(this IList<string> includeFields)
    {
        return (includeFields ?? []).Any(path =>
        {
            var field = GetItemField(path);
            return field.Equals(nameof(SalesRepCustomerDetails.PrimaryContact), StringComparison.OrdinalIgnoreCase)
                || field.Equals(nameof(SalesRepCustomerDetails.Phone), StringComparison.OrdinalIgnoreCase);
        });
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

    // The field of the customer itself: the first segment for a single customer, after `items` or `edges.node` for a list.
    private static string GetItemField(string path)
    {
        var segments = (path ?? string.Empty).Split('.');
        var start = segments[0] switch
        {
            "items" => 1,
            "edges" when segments.Length > 1 && segments[1] == "node" => 2,
            _ => 0,
        };

        return start < segments.Length ? segments[start] : string.Empty;
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
