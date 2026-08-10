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

        // Independent reads (orders store / catalog), so they overlap instead of adding up.
        var soldCategoriesTask = GetSoldCategoriesByTopLevelAsync(catalogId, context.ToScopeCriteria());
        var topLevelCategoriesTask = GetTopLevelCategoriesAsync(catalogId);
        await Task.WhenAll(soldCategoriesTask, topLevelCategoriesTask);

        var soldCategoriesByTopLevel = await soldCategoriesTask;

        return (await topLevelCategoriesTask)
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

        // Scope taken from the reader's criteria, so this hits the same cached lookup the discovery call populated.
        var soldCategoriesByTopLevel = await GetSoldCategoriesByTopLevelAsync(
            catalogId,
            SalesRepScopeCriteria.Create(criteria.OrganizationIds, criteria.CustomerId, criteria.StoreId, criteria.FromDate, criteria.ToDate));

        // Resolved against the badges GetRulesAsync offers, not merely against the catalog: a category the caller has
        // no sales in filters to nothing. Leaving it to an empty category set would read as "no category constraint"
        // downstream and answer the request with the unfiltered ranking.
        if (!soldCategoriesByTopLevel.TryGetValue(filter, out var categoryIds))
        {
            return null;
        }

        criteria.CategoryIds = categoryIds;

        return criteria;
    }

    /// <summary>
    /// Groups the categories the caller sold in by the top-level category they sit under in the store's catalog, using
    /// each category's outline for that catalog — which is what makes this work for a virtual store catalog: a physical
    /// category linked into it carries an outline like <c>store-catalog/top-level/category</c>. Categories outside the
    /// store's catalog (no such outline) are left out.
    /// </summary>
    protected virtual async Task<IDictionary<string, IList<string>>> GetSoldCategoriesByTopLevelAsync(string catalogId, SalesRepScopeCriteria criteria)
    {
        if (string.IsNullOrEmpty(catalogId))
        {
            return new Dictionary<string, IList<string>>();
        }

        var soldCategoryIds = await _topSellerService.GetSoldCategoryIdsAsync(criteria);
        if (soldCategoryIds.Count == 0)
        {
            return new Dictionary<string, IList<string>>();
        }

        var categories = await _categoryService.GetByIdsAsync(soldCategoryIds, nameof(CategoryResponseGroup.WithOutlines), catalogId);

        return categories
            .Select(x => new { CategoryId = x.Id, TopLevelId = GetTopLevelCategoryId(x, catalogId) })
            .Where(x => !string.IsNullOrEmpty(x.TopLevelId))
            .GroupBy(x => x.TopLevelId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                // Ordered so the same badge always yields the same criteria — the ranking is cached on them.
                g => (IList<string>)g.Select(x => x.CategoryId).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                StringComparer.OrdinalIgnoreCase);
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
