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
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepTopSellerFilterRuleResolver : FilterRuleResolverBase<SalesRepTopSellerFilterRule>, ISalesRepTopSellerFilterRuleResolver
{
    protected const int CategorySearchPageSize = 50;

    private readonly IStoreService _storeService;
    private readonly ICategorySearchService _categorySearchService;
    private readonly IProductIndexedSearchService _productIndexedSearchService;
    private readonly ISalesRepTopSellerService _topSellerService;

    public SalesRepTopSellerFilterRuleResolver(
        IStoreService storeService,
        ICategorySearchService categorySearchService,
        IProductIndexedSearchService productIndexedSearchService,
        ISalesRepTopSellerService topSellerService)
    {
        _storeService = storeService;
        _categorySearchService = categorySearchService;
        _productIndexedSearchService = productIndexedSearchService;
        _topSellerService = topSellerService;
    }

    /// <summary>
    /// One rule per top-level category of the store's catalog that actually contains products — a category whose
    /// subtree has no product would only ever yield an empty Top Sellers list, so it is not offered as a badge.
    /// </summary>
    public override async Task<IList<SalesRepTopSellerFilterRule>> GetRulesAsync(SalesRepFilterRuleContext context)
    {
        var catalogId = await GetStoreCatalogIdAsync(context.StoreId);
        var topLevelCategories = await GetTopLevelCategoriesAsync(catalogId);

        var rules = new List<SalesRepTopSellerFilterRule>(topLevelCategories.Count);

        foreach (var category in topLevelCategories)
        {
            if (await HasProductsAsync(catalogId, category.Id))
            {
                rules.Add(SalesRepTopSellerFilterRule.Create(category.Id, category.Name));
            }
        }

        return rules;
    }

    /// <summary>
    /// Whether the category's subtree holds at least one product, asked of the catalog index exactly the way
    /// <see cref="ApplyListFilterAsync"/> asks it (same outline convention), so a badge is offered if and only if
    /// selecting it can match products. <c>Take = 0</c> keeps it a count-only request.
    /// </summary>
    protected virtual async Task<bool> HasProductsAsync(string catalogId, string categoryId)
    {
        var searchCriteria = AbstractTypeFactory<ProductIndexedSearchCriteria>.TryCreateInstance();
        searchCriteria.CatalogId = catalogId;
        searchCriteria.Outline = categoryId;
        searchCriteria.Take = 0;

        var result = await _productIndexedSearchService.SearchAsync(searchCriteria);

        return result.TotalCount > 0;
    }

    public virtual async Task<SalesRepTopSellerCriteria> ApplyListFilterAsync(string storeId, string filter, SalesRepTopSellerCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria;
        }

        var catalogId = await GetStoreCatalogIdAsync(storeId);
        if (string.IsNullOrEmpty(catalogId))
        {
            return null;
        }

        // Resolution deliberately looks at every top-level category, not only the non-empty ones GetRulesAsync offers:
        // an empty category yields an empty list anyway, and skipping the per-category index probes keeps applying a
        // filter a single search.
        var category = await ResolveCategoryAsync(catalogId, filter);
        if (category == null)
        {
            return null;
        }

        var candidateProductIds = await _topSellerService.GetSoldProductIdsAsync(criteria);
        if (candidateProductIds.Count == 0)
        {
            criteria.ProductIds = [];
            return criteria;
        }

        var searchCriteria = AbstractTypeFactory<ProductIndexedSearchCriteria>.TryCreateInstance();
        searchCriteria.CatalogId = catalogId;
        searchCriteria.Outline = category.Id;
        searchCriteria.ObjectIds = candidateProductIds.ToArray();
        searchCriteria.Take = candidateProductIds.Count;

        var result = await _productIndexedSearchService.SearchAsync(searchCriteria);

        criteria.ProductIds = result.Items?.Select(x => x.Id).ToList() ?? [];
        return criteria;
    }

    protected virtual async Task<Category> ResolveCategoryAsync(string catalogId, string filter)
    {
        var categories = await GetTopLevelCategoriesAsync(catalogId);
        return categories.FirstOrDefault(x => string.Equals(x.Id, filter, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual async Task<IList<Category>> GetTopLevelCategoriesAsync(string catalogId)
    {
        var categories = await GetCatalogCategoriesAsync(catalogId);

        return categories
            .Where(x => IsTopLevel(x) && x.IsActive != false)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<string> GetStoreCatalogIdAsync(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await _storeService.GetByIdAsync(storeId);
        return store?.Catalog;
    }

    private async Task<IList<Category>> GetCatalogCategoriesAsync(string catalogId)
    {
        if (string.IsNullOrEmpty(catalogId))
        {
            return [];
        }

        var criteria = AbstractTypeFactory<CategorySearchCriteria>.TryCreateInstance();
        criteria.CatalogId = catalogId;
        criteria.Take = CategorySearchPageSize;
        return await _categorySearchService.SearchAllNoCloneAsync(criteria);
    }

    private static bool IsTopLevel(Category category) => string.IsNullOrEmpty(category.ParentId);
}
