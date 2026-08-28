using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepProductResolver : ISalesRepProductResolver
{
    private static readonly string _responseGroup =
        (ItemResponseGroup.ItemInfo | ItemResponseGroup.WithImages).ToString();

    private readonly IProductSearchService _productSearchService;
    private readonly IStoreService _storeService;

    public SalesRepProductResolver(IProductSearchService productSearchService, IStoreService storeService)
    {
        _productSearchService = productSearchService;
        _storeService = storeService;
    }

    // Analytics carries the product CODE (GA itemId); an unresolvable code simply stays absent from the map.
    public virtual async Task<IDictionary<string, SalesRepActivityProduct>> ResolveByCodesAsync(IList<string> codes, string storeId)
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
        // A code is unique within a catalog, not across them: without this the same SKU in a second catalog could
        // answer, and the rep would see a foreign product's name, image and link for their customer's activity.
        criteria.CatalogId = await GetStoreCatalogIdAsync(storeId);
        criteria.Take = codesToSearch.Count;
        criteria.ResponseGroup = _responseGroup;

        var searchResult = await _productSearchService.SearchAsync(criteria);

        foreach (var product in searchResult.Results.Where(x => !string.IsNullOrEmpty(x.Code)))
        {
            result.TryAdd(product.Code, ToActivityProduct(product));
        }

        return result;
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
        return store?.Catalog;
    }
}
