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

    // Analytics carries the product CODE (GA itemId); an unresolvable code simply stays absent from the map.
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
        // A code is unique within a catalog, not across them, so the store's catalog is what makes a code an
        // answer. Without a storeId there is no catalog to narrow by and ambiguity is handled below instead.
        criteria.CatalogId = await GetStoreCatalogIdAsync(storeId);
        criteria.Take = codesToSearch.Count;
        criteria.ResponseGroup = _responseGroup;

        var searchResult = await _productSearchService.SearchAsync(criteria);

        // One row per code holds only while a catalog scopes the search. Without one a code can match a product
        // per catalog, overflowing the page — and a code that looks unique in a truncated page may not be. Trust
        // the page only when it carries every match.
        if (searchResult.TotalCount > criteria.Take)
        {
            return result;
        }

        foreach (var group in searchResult.Results
                     .Where(x => !string.IsNullOrEmpty(x.Code))
                     .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            // A code matching several catalog products cannot be attributed to one of them, so it stays
            // unresolved: the caller keeps the name analytics tracked and a null product id, exactly as for a code
            // no catalog carries any more. Guessing would put another catalog's name, image and deep link on the
            // rep's screen as their customer's activity.
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
        return store?.Catalog;
    }
}
