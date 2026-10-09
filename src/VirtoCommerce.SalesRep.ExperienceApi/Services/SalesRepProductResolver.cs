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

    private static readonly string _outlinesResponseGroup = ItemResponseGroup.Outlines.ToString();

    // Headroom over one row per code, so a code carried by a few catalogs still comes back complete.
    private const int MaxMatchesPerCode = 5;

    private readonly IProductSearchService _productSearchService;
    private readonly IStoreService _storeService;
    private readonly ICatalogService _catalogService;
    private readonly IItemService _itemService;

    public SalesRepProductResolver(
        IProductSearchService productSearchService,
        IStoreService storeService,
        ICatalogService catalogService,
        IItemService itemService)
    {
        _productSearchService = productSearchService;
        _storeService = storeService;
        _catalogService = catalogService;
        _itemService = itemService;
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

        var storeCatalogId = await GetStoreCatalogIdAsync(storeId);
        var isVirtualCatalog = await IsVirtualCatalogAsync(storeCatalogId);

        var criteria = AbstractTypeFactory<ProductSearchCriteria>.TryCreateInstance();
        criteria.Skus = codesToSearch;
        // A code is unique within a catalog, not across them. A VIRTUAL catalog holds links, not products, and product
        // search matches an item's own CatalogId — narrowing by one would resolve NOTHING, so such a store (the common
        // B2B setup) is not narrowed and an ambiguous code is settled by what the store shows, below.
        criteria.CatalogId = isVirtualCatalog ? null : storeCatalogId;
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

        var matchesByCode = searchResult.Results
            .Where(x => !string.IsNullOrEmpty(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The customer viewed the product in THIS store, so of a code's matches only those the store shows count.
        var shownIds = isVirtualCatalog
            ? await GetIdsShownInCatalogAsync(matchesByCode.Where(x => x.Count() > 1).SelectMany(x => x).ToList(), storeCatalogId)
            : null;

        foreach (var matches in matchesByCode)
        {
            var candidates = matches.Count() > 1 && shownIds != null
                ? matches.Where(x => shownIds.Contains(x.Id)).ToList()
                : matches.ToList();

            // Still ambiguous, so unresolved — as for a code no catalog carries: guessing would show another catalog's
            // name, image and link as this customer's activity.
            if (candidates.Count == 1)
            {
                result[matches.Key] = ToActivityProduct(candidates[0]);
            }
        }

        return result;
    }

    // A product a virtual catalog shows has an outline that starts in it — linked in itself or through a category.
    protected virtual async Task<ISet<string>> GetIdsShownInCatalogAsync(IList<CatalogProduct> products, string catalogId)
    {
        if (products.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var withOutlines = await _itemService.GetByIdsAsync(products.Select(x => x.Id).ToList(), _outlinesResponseGroup, catalogId);

        return withOutlines
            .Where(x => !x.Outlines.IsNullOrEmpty())
            .Select(x => x.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        return store?.Catalog.EmptyToNull();
    }

    protected virtual async Task<bool> IsVirtualCatalogAsync(string catalogId)
    {
        if (string.IsNullOrEmpty(catalogId))
        {
            return false;
        }

        var catalog = await _catalogService.GetNoCloneAsync(catalogId);

        return catalog?.IsVirtual == true;
    }
}
