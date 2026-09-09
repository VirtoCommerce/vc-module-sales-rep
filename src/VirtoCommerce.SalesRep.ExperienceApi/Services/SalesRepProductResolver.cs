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

    // Headroom over one row per code, so that a code carried by a few catalogs still comes back complete and is
    // answered per row. Past it the page is truncated and no code's match set is known, which is the only case
    // that still has to give up on the whole batch.
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
        // Analytics tracks what the storefront sent as item_id, which for a catalog that sells by size or pack is
        // the VARIATION's code. A product search excludes variations unless asked, so without this every one of
        // those codes came back unresolved — a raw SKU where a name, an image and a link belong.
        criteria.SearchInVariations = true;
        criteria.Take = codesToSearch.Count * MaxMatchesPerCode;
        criteria.ResponseGroup = _responseGroup;

        var searchResult = await _productSearchService.SearchAsync(criteria);

        // A code that looks unique in a TRUNCATED page may not be, so a page that does not carry every match
        // cannot answer for any code on it. Within a complete page each code is answered on its own rows below.
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
        var catalogId = store?.Catalog;
        if (string.IsNullOrEmpty(catalogId))
        {
            return null;
        }

        // A VIRTUAL catalog holds links to products, not products: a product search matches an item's own
        // CatalogId, so narrowing by one matches nothing and EVERY code comes back unresolved — which is what
        // a rep sees as a raw SKU where a product name and a link belong. A store built that way (the common
        // B2B setup) cannot be narrowed by catalog here at all, so it is not narrowed: the ambiguity rule
        // above is what keeps the answer honest, and a code carried by exactly one catalog resolves as it did
        // before the narrowing existed.
        var catalog = await _catalogService.GetNoCloneAsync(catalogId);

        return catalog?.IsVirtual == true ? null : catalogId;
    }
}
