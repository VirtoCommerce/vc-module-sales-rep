using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class IncludeFieldsExtensions
{
    /// <summary>
    /// True when the GraphQL selection paths contain <paramref name="fieldName"/> as ANY segment (not just the
    /// leaf) — so an object-valued field like <c>address { city }</c> is recognized by its "address" segment,
    /// while a different segment (e.g. the connection's own <c>totalCount</c>) never matches. The single
    /// definition of "was this field requested", shared by the customer response-group parser and the handlers'
    /// secondary-load gating so the two can't drift.
    /// </summary>
    public static bool IncludesField(this IList<string> includeFields, string fieldName)
    {
        return (includeFields ?? []).Any(path => !string.IsNullOrEmpty(path)
            && path.Split('.').Any(segment => segment.Equals(fieldName, StringComparison.OrdinalIgnoreCase)));
    }
}
