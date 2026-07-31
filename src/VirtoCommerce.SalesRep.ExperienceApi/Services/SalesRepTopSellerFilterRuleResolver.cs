using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Extensions;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Outlines;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
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
    private readonly ICategoryService _categoryService;
    private readonly ISalesRepTopSellerService _topSellerService;

    public SalesRepTopSellerFilterRuleResolver(
        IStoreService storeService,
        ICategorySearchService categorySearchService,
        ICategoryService categoryService,
        ISalesRepTopSellerService topSellerService)
    {
        _storeService = storeService;
        _categorySearchService = categorySearchService;
        _categoryService = categoryService;
        _topSellerService = topSellerService;
    }

    /// <summary>
    /// One rule per top-level category of the store's catalog the caller actually sold into — within their scope
    /// (served organizations, own created orders) and the selected period, i.e. exactly the records the Top Sellers list
    /// ranks. A category with products but no sales in that window is not offered, so selecting a badge can never
    /// produce an empty list.
    /// </summary>
    public override async Task<IList<SalesRepTopSellerFilterRule>> GetRulesAsync(SalesRepFilterRuleContext context)
    {
        var catalogId = await GetStoreCatalogIdAsync(context.StoreId);

        var soldCategoriesByTopLevel = await GetSoldCategoriesByTopLevelAsync(catalogId, BuildSoldCategoriesCriteria(context));
        if (soldCategoriesByTopLevel.Count == 0)
        {
            return [];
        }

        var topLevelCategories = await GetTopLevelCategoriesAsync(catalogId);

        return topLevelCategories
            .Where(x => soldCategoriesByTopLevel.ContainsKey(x.Id))
            .Select(x => SalesRepTopSellerFilterRule.Create(x.Id, x.Name))
            .ToList();
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

        // Resolution deliberately looks at every top-level category, not only the sold-into ones GetRulesAsync offers:
        // a category with no sales in this window resolves to an empty category set, which the ranking reads as "no
        // rows" anyway.
        var category = await ResolveCategoryAsync(catalogId, filter);
        if (category == null)
        {
            return null;
        }

        // The ranking filters line items by their own category snapshot (SalesRepTopSellerService.BuildQuery), so the
        // whole subtree collapses to the categories the caller actually sold in — no product-id list to carry, and the
        // work stays proportional to the catalog structure rather than to the number of products ever sold.
        var soldCategoriesByTopLevel = await GetSoldCategoriesByTopLevelAsync(catalogId, BuildSoldCategoriesCriteria(criteria));

        criteria.CategoryIds = soldCategoriesByTopLevel.TryGetValue(category.Id, out var categoryIds)
            ? categoryIds
            : [];

        return criteria;
    }

    /// <summary>
    /// Groups the categories the caller sold in by the top-level category they sit under in the store's catalog, using
    /// each category's outline for that catalog — which is what makes this work for a virtual store catalog: a physical
    /// category linked into it carries an outline like <c>store-catalog/top-level/category</c>. Categories outside the
    /// store's catalog (no such outline) are left out.
    /// </summary>
    protected virtual async Task<IDictionary<string, IList<string>>> GetSoldCategoriesByTopLevelAsync(string catalogId, SalesRepSoldCategoryCriteria criteria)
    {
        var result = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(catalogId))
        {
            return result;
        }

        var soldCategoryIds = await _topSellerService.GetSoldCategoryIdsAsync(criteria);
        if (soldCategoryIds.Count == 0)
        {
            return result;
        }

        var categories = await _categoryService.GetByIdsAsync(soldCategoryIds, nameof(CategoryResponseGroup.WithOutlines), catalogId);

        foreach (var category in categories)
        {
            var topLevelId = GetTopLevelCategoryId(category, catalogId);
            if (string.IsNullOrEmpty(topLevelId))
            {
                continue;
            }

            if (!result.TryGetValue(topLevelId, out var categoryIds))
            {
                categoryIds = [];
                result[topLevelId] = categoryIds;
            }

            categoryIds.Add(category.Id);
        }

        return result;
    }

    protected virtual string GetTopLevelCategoryId(Category category, string catalogId)
    {
        if (!category.Outlines.TryGetOutlineForCatalog(catalogId, out var outline))
        {
            return null;
        }

        // Outline is catalog/top-level/…/category; the first non-catalog item is the top-level category (which is the
        // category itself when it sits directly under the catalog).
        return outline.Items?.FirstOrDefault(x => !x.IsCatalog())?.Id;
    }

    protected virtual SalesRepSoldCategoryCriteria BuildSoldCategoriesCriteria(SalesRepFilterRuleContext context)
        => BuildSoldCategoriesCriteria(context.OrganizationIds, context.CustomerId, context.StoreId, context.FromDate, context.ToDate);

    /// <summary>Same scope, taken from the reader's criteria — so discovering the badges and applying one hit the same
    /// cached lookup instead of two keys holding identical data.</summary>
    protected virtual SalesRepSoldCategoryCriteria BuildSoldCategoriesCriteria(SalesRepTopSellerCriteria criteria)
        => BuildSoldCategoriesCriteria(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId, criteria.FromDate, criteria.ToDate);

    private static SalesRepSoldCategoryCriteria BuildSoldCategoriesCriteria(
        IList<string> organizationIds,
        string customerId,
        string storeId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var criteria = AbstractTypeFactory<SalesRepSoldCategoryCriteria>.TryCreateInstance();
        criteria.OrganizationIds = organizationIds;
        criteria.CustomerId = customerId;
        criteria.StoreId = storeId;
        criteria.FromDate = fromDate;
        criteria.ToDate = toDate;
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
