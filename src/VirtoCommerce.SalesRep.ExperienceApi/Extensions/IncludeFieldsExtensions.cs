using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class IncludeFieldsExtensions
{
    public static bool IncludesField(this IList<string> includeFields, string fieldName)
    {
        return (includeFields ?? []).Any(path => !string.IsNullOrEmpty(path)
            && path.Split('.').Any(segment => segment.Equals(fieldName, StringComparison.OrdinalIgnoreCase)));
    }
}
