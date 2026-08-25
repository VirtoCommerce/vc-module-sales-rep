using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Seo.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepProductResolver : ISalesRepProductResolver
{
    private static readonly string _responseGroup =
        (ItemResponseGroup.ItemInfo | ItemResponseGroup.WithImages | ItemResponseGroup.WithSeo).ToString();

    private readonly IProductSearchService _productSearchService;

    public SalesRepProductResolver(IProductSearchService productSearchService)
    {
        _productSearchService = productSearchService;
    }

    // Analytics carries the product CODE (GA itemId); an unresolvable code simply stays absent from the map.
    public virtual async Task<IDictionary<string, SalesRepActivityProduct>> ResolveByCodesAsync(IList<string> codes, string storeId, string cultureName)
    {
        var result = new Dictionary<string, SalesRepActivityProduct>(StringComparer.OrdinalIgnoreCase);

        var codesToSearch = (codes ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codesToSearch.Count == 0)
        {
            return result;
        }

        var criteria = AbstractTypeFactory<ProductSearchCriteria>.TryCreateInstance();
        criteria.Skus = codesToSearch;
        criteria.Take = codesToSearch.Count;
        criteria.ResponseGroup = _responseGroup;

        var searchResult = await _productSearchService.SearchAsync(criteria);

        foreach (var product in searchResult.Results.Where(x => !string.IsNullOrEmpty(x.Code)))
        {
            result.TryAdd(product.Code, ToActivityProduct(product, storeId, cultureName));
        }

        return result;
    }

    protected virtual SalesRepActivityProduct ToActivityProduct(CatalogProduct product, string storeId, string cultureName)
    {
        var result = AbstractTypeFactory<SalesRepActivityProduct>.TryCreateInstance();

        result.Code = product.Code;
        result.ProductId = product.Id;
        result.Name = product.Name;
        result.Slug = product.SeoInfos?.GetBestMatchingSeoInfo(storeId, cultureName, cultureName)?.SemanticUrl;
        result.ImageUrl = product.ImgSrc;

        return result;
    }
}
