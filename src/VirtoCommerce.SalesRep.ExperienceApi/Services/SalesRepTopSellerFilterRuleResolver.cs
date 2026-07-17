using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default "Top Sellers" category-badge source: each top-level non-hidden category of the store's catalog, 1:1
/// (the rule name is the category id). A selected category expands to its subtree (itself + all descendants) so the
/// ranking counts line items filed under any nested category, not just the exact one. Reads the catalog via
/// <see cref="ICategorySearchService"/> and the store's catalog via <see cref="IStoreService"/> — the module's only
/// catalog dependency. A project replaces this service (DI last-registration wins) to group categories or add rules.
/// </summary>
public class SalesRepTopSellerFilterRuleResolver : ISalesRepTopSellerFilterRuleResolver
{
    private readonly IStoreService _storeService;
    private readonly ICategorySearchService _categorySearchService;

    public SalesRepTopSellerFilterRuleResolver(IStoreService storeService, ICategorySearchService categorySearchService)
    {
        _storeService = storeService;
        _categorySearchService = categorySearchService;
    }

    public virtual async Task<IList<SalesRepTopSellerFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        var categories = await GetCatalogCategoriesAsync(storeId);

        return categories
            .Where(x => IsTopLevel(x) && x.IsActive != false)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => SalesRepTopSellerFilterRule.Create(x.Id, x.Name))
            .ToList();
    }

    public virtual async Task<SalesRepTopSellerCriteria> ApplyFilterAsync(string storeId, string filter, SalesRepTopSellerCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria; // no category constraint — all categories
        }

        var categories = await GetCatalogCategoriesAsync(storeId);

        // The selection must be a recognized non-hidden top-level category; otherwise fail closed.
        var root = categories.FirstOrDefault(x =>
            string.Equals(x.Id, filter, StringComparison.OrdinalIgnoreCase) && IsTopLevel(x) && x.IsActive != false);
        if (root == null)
        {
            return null;
        }

        criteria.CategoryIds = GetSubtreeIds(root.Id, categories).ToArray();
        return criteria;
    }

    private async Task<IList<Category>> GetCatalogCategoriesAsync(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return [];
        }

        var store = await _storeService.GetByIdAsync(storeId);
        if (string.IsNullOrEmpty(store?.Catalog))
        {
            return [];
        }

        var criteria = AbstractTypeFactory<CategorySearchCriteria>.TryCreateInstance();
        criteria.CatalogId = store.Catalog;
        criteria.Take = 50; // page size for SearchAllNoCloneAsync (it pages through the whole catalog tree)
        return await _categorySearchService.SearchAllNoCloneAsync(criteria);
    }

    // A top-level category has no parent within the catalog.
    private static bool IsTopLevel(Category category) => string.IsNullOrEmpty(category.ParentId);

    // The category and all its descendants, walked over ParentId (the catalog tree is small; one in-memory pass).
    private static IEnumerable<string> GetSubtreeIds(string rootId, IList<Category> categories)
    {
        var childrenByParent = categories
            .Where(x => !string.IsNullOrEmpty(x.ParentId))
            .GroupBy(x => x.ParentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!result.Add(id))
            {
                continue;
            }

            if (childrenByParent.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }
}
