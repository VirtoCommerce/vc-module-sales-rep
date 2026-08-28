using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public static class SalesRepActivityCategoryExtensions
{
    public static bool IsCategoryRequested(this SalesRepActivitySearchCriteria criteria, string category)
    {
        return criteria.Categories.IsNullOrEmpty() || criteria.Categories.ContainsIgnoreCase(category);
    }
}
