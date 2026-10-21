using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

// Lives beside ISalesRepActivitySource rather than with the shipped sources: a source contributed from another
// assembly references .Core, and would otherwise have to take a .Data reference to read the caller's filter.
public static class SalesRepActivityCategoryExtensions
{
    public static bool IsCategoryRequested(this SalesRepActivitySearchCriteria criteria, string category)
    {
        return criteria.Categories.IsNullOrEmpty() || criteria.Categories.ContainsIgnoreCase(category);
    }
}
