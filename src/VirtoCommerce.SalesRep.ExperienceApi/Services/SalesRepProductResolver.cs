using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepProductResolver : ISalesRepProductResolver
{
    private static readonly string _responseGroup =
        (ItemResponseGroup.ItemInfo | ItemResponseGroup.WithImages).ToString();

    // Headroom over one row per code, so a code carried by a few catalogs still comes back complete.
    private const int MaxMatchesPerCode = 5;

    private readonly IProductSearchService _productSearchService;
    private readonly IStoreService _storeService;
    private readonly ICatalogService _catalogService;

    public SalesRepProductResolver(
        IProductSearchService productSearchService,
        IStoreService storeService,
        ICatalogService catalogService)
    {
        _productSearchService = productSearchService;
        _storeService = storeService;
        _catalogService = catalogService;
    }

    public virtual async Task ResolveAsync<T>(IList<T> rows, string storeId, Func<T, string> getCode, Action<T, SalesRepActivityProduct> setProduct)
    {
        if (rows.IsNullOrEmpty())
        {
            return;
        }

        var productsByCode = await ResolveByCodesAsync(rows.Select(getCode).ToList(), storeId);

        foreach (var row in rows)
        {
            var code = getCode(row);
            // Guarded rather than passed straight to TryGetValue: a null key throws on this dictionary.
            if (!string.IsNullOrEmpty(code) && productsByCode.TryGetValue(code, out var product))
            {
                setProduct(row, product);
            }
        }
    }

    protected virtual async Task<IDictionary<string, SalesRepActivityProduct>> ResolveByCodesAsync(IList<string> codes, string storeId)
    {
        var result = new Dictionary<string, SalesRepActivityProduct>(StringComparer.OrdinalIgnoreCase);

        var codesToSearch = (codes ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .DistinctIgnoreCase()
            .ToList();
        if (codesToSearch.Count == 0)
        {
            return result;
        }

        var criteria = AbstractTypeFactory<ProductSearchCriteria>.TryCreateInstance();
        criteria.Skus = codesToSearch;
        // A code is unique within a catalog, not across them; without a storeId the ambiguity rule below decides.
        criteria.CatalogId = await GetStoreCatalogIdAsync(storeId);
        // GA item_id is often a VARIATION's code (size, pack), and a product search skips variations unless asked.
        criteria.SearchInVariations = true;
        criteria.Take = codesToSearch.Count * MaxMatchesPerCode;
        criteria.ResponseGroup = _responseGroup;

        var searchResult = await _productSearchService.SearchAsync(criteria);

        // A code that looks unique in a TRUNCATED page may not be, so such a page answers for nothing.
        if (searchResult.TotalCount > criteria.Take)
        {
            return result;
        }

        foreach (var group in searchResult.Results
                     .Where(x => !string.IsNullOrEmpty(x.Code))
                     .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            // Ambiguous, so unresolved — as for a code no catalog carries: guessing would show another catalog's name,
            // image and link as this customer's activity.
            if (group.Count() == 1)
            {
                result[group.Key] = ToActivityProduct(group.First());
            }
        }

        return result;
    }

    protected virtual SalesRepActivityProduct ToActivityProduct(CatalogProduct product)
    {
        var result = AbstractTypeFactory<SalesRepActivityProduct>.TryCreateInstance();

        result.Code = product.Code;
        result.ProductId = product.Id;
        result.Name = product.Name;
        result.ImageUrl = product.ImgSrc;

        return result;
    }

    protected virtual async Task<string> GetStoreCatalogIdAsync(string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await _storeService.GetNoCloneAsync(storeId);
        var catalogId = store?.Catalog;
        if (string.IsNullOrEmpty(catalogId))
        {
            return null;
        }

        // A VIRTUAL catalog holds links, not products, and product search matches an item's own CatalogId — so
        // narrowing by one resolves NOTHING. Such a store (the common B2B setup) is not narrowed; the ambiguity
        // rule above keeps the answer honest.
        var catalog = await _catalogService.GetNoCloneAsync(catalogId);

        return catalog?.IsVirtual == true ? null : catalogId;
    }
}
