using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public static class SalesRepActivityCategoryExtensions
{
    public static IList<string> GetEffectiveCategories(this SalesRepActivitySearchCriteria criteria, IList<string> sourceCategories)
    {
        var categories = sourceCategories ?? [];

        return criteria.Categories.IsNullOrEmpty()
            ? categories.ToList()
            : categories.Where(x => criteria.Categories.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
    }
}
