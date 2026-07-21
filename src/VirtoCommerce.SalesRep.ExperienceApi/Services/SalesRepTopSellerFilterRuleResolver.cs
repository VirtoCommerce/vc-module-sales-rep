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

/// <summary>
/// Default "Top Sellers" category-badge source: each top-level non-hidden category of the store's catalog, 1:1
/// (the rule name is the category id). On selection the category is resolved to the rep's sold products that fall
/// in its subtree via the catalog's own indexed product search (<see cref="IProductIndexedSearchService"/>) — the
/// same mechanism the storefront's category pages use — and the ranking is restricted to those products. This is
/// the only correct membership source for a <b>virtual</b> store catalog: a line item snapshots the <i>physical</i>
/// category id, so matching by the line-item category (the previous subtree-id approach) never hits a virtual store
/// category. The index lookup is bounded by the rep's own sold products (via <see cref="ISalesRepTopSellerService"/>)
/// so it never enumerates a whole category and the data-isolation invariant stays intact. Reads the catalog via
/// <see cref="ICategorySearchService"/> and the store's catalog via <see cref="IStoreService"/>. A project replaces
/// this service (DI last-registration wins) to group categories or add rules.
/// </summary>
public class SalesRepTopSellerFilterRuleResolver : FilterRuleResolverBase<SalesRepTopSellerFilterRule>, ISalesRepTopSellerFilterRuleResolver
{
    /// <summary>Page size used to page the catalog category tree via <c>SearchAllNoCloneAsync</c>.</summary>
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

    public override async Task<IList<SalesRepTopSellerFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        var catalogId = await GetStoreCatalogIdAsync(storeId);
        var categories = await GetCatalogCategoriesAsync(catalogId);

        return categories
            .Where(x => IsTopLevel(x) && x.IsActive != false)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => SalesRepTopSellerFilterRule.Create(x.Id, x.Name))
            .ToList();
    }

    public virtual async Task<SalesRepTopSellerCriteria> ApplyListFilterAsync(string storeId, string filter, SalesRepTopSellerCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria; // no category constraint — all categories
        }

        // Resolve the selected badge through the same rule set the discovery query exposes, so a project that hides
        // or regroups categories via GetRulesAsync changes what this accepts too. Unrecognized → fail closed.
        var rule = await ResolveNamedRuleAsync(storeId, filter);
        if (rule == null)
        {
            return null;
        }

        var catalogId = await GetStoreCatalogIdAsync(storeId);
        if (string.IsNullOrEmpty(catalogId))
        {
            return null; // no store catalog to resolve the category against — fail closed
        }

        // Bound the index lookup by the rep's own sold products in scope (creator scope already applied by the
        // service), so the catalog index is never asked to enumerate a whole category — and cross-rep products can
        // never leak in (data-isolation invariant).
        var candidateProductIds = await _topSellerService.GetSoldProductIdsAsync(criteria);
        if (candidateProductIds.Count == 0)
        {
            criteria.ProductIds = [];
            return criteria;
        }

        // Resolve "which of those products are in the selected category's subtree" via the catalog index (per-item
        // links + CategoryRelation + subtree outlines), which works for virtual and physical store catalogs alike.
        // GetOutlines() prepends CatalogId to Outline, so a bare category id yields the "{catalogId}/{categoryId}"
        // outline term whose prefix match selects the whole subtree.
        var searchCriteria = AbstractTypeFactory<ProductIndexedSearchCriteria>.TryCreateInstance();
        searchCriteria.CatalogId = catalogId;
        searchCriteria.Outline = rule.Name; // the rule name is the category id
        searchCriteria.ObjectIds = candidateProductIds.ToArray();
        searchCriteria.Take = candidateProductIds.Count;

        var result = await _productIndexedSearchService.SearchAsync(searchCriteria);

        criteria.ProductIds = result.Items?.Select(x => x.Id).ToArray() ?? [];
        return criteria;
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
        criteria.Take = CategorySearchPageSize; // page size for SearchAllNoCloneAsync (it pages through the whole catalog tree)
        return await _categorySearchService.SearchAllNoCloneAsync(criteria);
    }

    // A top-level category has no parent within the catalog.
    private static bool IsTopLevel(Category category) => string.IsNullOrEmpty(category.ParentId);
}
