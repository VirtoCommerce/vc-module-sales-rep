using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public static class SalesRepActivityCategoryExtensions
{
    public static IList<string> GetEffectiveCategories(this SalesRepActivitySearchCriteria criteria, IList<string> sourceCategories)
    {
        return (sourceCategories ?? []).Where(criteria.IsCategoryRequested).ToList();
    }

    public static bool IsCategoryRequested(this SalesRepActivitySearchCriteria criteria, string category)
    {
        return criteria.Categories.IsNullOrEmpty() || criteria.Categories.ContainsIgnoreCase(category);
    }
}
