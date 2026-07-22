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
            return criteria;
        }

        var rule = await ResolveNamedRuleAsync(storeId, filter);
        if (rule == null)
        {
            return null;
        }

        var catalogId = await GetStoreCatalogIdAsync(storeId);
        if (string.IsNullOrEmpty(catalogId))
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
        searchCriteria.Outline = rule.Name;
        searchCriteria.ObjectIds = candidateProductIds.ToArray();
        searchCriteria.Take = candidateProductIds.Count;

        var result = await _productIndexedSearchService.SearchAsync(searchCriteria);

        criteria.ProductIds = result.Items?.Select(x => x.Id).ToList() ?? [];
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
        criteria.Take = CategorySearchPageSize;
        return await _categorySearchService.SearchAllNoCloneAsync(criteria);
    }

    private static bool IsTopLevel(Category category) => string.IsNullOrEmpty(category.ParentId);
}
