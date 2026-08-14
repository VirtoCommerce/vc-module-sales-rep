using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Caching;

namespace VirtoCommerce.SalesRep.Data.Services;

// Listing/paging works off the AssetEntry DB index (never IBlobStorageProvider.SearchAsync — no paging there).
// The library is small (tens–hundreds), so all entries of the Group are fetched via the search-all pattern,
// joined with their metadata rows, and the whole mapped set cached (TTL setting + expired on module mutations).
public class SalesRepDocumentSearchService : ISalesRepDocumentSearchService
{
    private const int SearchAllPageSize = 100;
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    private readonly IAssetEntrySearchService _assetEntrySearchService;
    private readonly ISalesRepDocumentMetadataService _metadataService;
    private readonly IPlatformMemoryCache _platformMemoryCache;
    private readonly ISettingsManager _settingsManager;

    public SalesRepDocumentSearchService(
        IAssetEntrySearchService assetEntrySearchService,
        ISalesRepDocumentMetadataService metadataService,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager)
    {
        _assetEntrySearchService = assetEntrySearchService;
        _metadataService = metadataService;
        _platformMemoryCache = platformMemoryCache;
        _settingsManager = settingsManager;
    }

    public virtual async Task<SalesRepDocumentSearchResult> SearchAsync(SalesRepDocumentSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var documents = (await GetAllDocumentsAsync()).AsEnumerable();

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            documents = documents.Where(x => criteria.ObjectIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(criteria.Category))
        {
            documents = documents.Where(x => criteria.Category.EqualsIgnoreCase(x.Category));
        }

        if (criteria.IsPinned != null)
        {
            documents = documents.Where(x => x.IsPinned == criteria.IsPinned);
        }

        documents = ApplyKeyword(documents, criteria.Keyword);

        var matched = ApplySort(documents, criteria.SortInfos).ToList();

        var result = AbstractTypeFactory<SalesRepDocumentSearchResult>.TryCreateInstance();
        result.TotalCount = matched.Count;
        result.Results = matched
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .Select(x => x.CloneTyped()) // the matched instances belong to the cache
            .ToList();

        return result;
    }

    public virtual async Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync(string keyword = null)
    {
        var documents = ApplyKeyword(await GetAllDocumentsAsync(), keyword);

        return documents
            .Where(x => !string.IsNullOrEmpty(x.Category))
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var category = AbstractTypeFactory<SalesRepDocumentCategory>.TryCreateInstance();
                category.Name = group.Key;
                category.Count = group.Count();
                return category;
            })
            .ToList();
    }

    protected virtual Task<IList<SalesRepDocument>> GetAllDocumentsAsync()
    {
        var cacheKey = CacheKey.With(GetType(), nameof(GetAllDocumentsAsync));

        return _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async options =>
        {
            options.AddExpirationToken(SalesRepDocumentCacheRegion.CreateChangeToken());

            var minutes = await _settingsManager.GetValueAsync<int>(ModuleConstants.Settings.Caching.DocumentsCacheExpiration);
            options.AbsoluteExpirationRelativeToNow = minutes > 0 ? TimeSpan.FromMinutes(minutes) : CacheDisabled;

            var criteria = AbstractTypeFactory<AssetEntrySearchCriteria>.TryCreateInstance();
            criteria.Group = ModuleConstants.DocumentsScope;
            criteria.Take = SearchAllPageSize;
            criteria.Sort = "createdDate:desc"; // the search-all loop requires an explicit sort

            var entries = await _assetEntrySearchService.SearchAllAsync(criteria);
            var metadataById = (await _metadataService.GetByIdsAsync(entries.Select(x => x.Id).ToList()))
                .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

            return (IList<SalesRepDocument>)entries
                .Select(x => SalesRepDocumentMapper.ToModel(x, metadataById.GetValueOrDefault(x.Id)))
                .ToList();
        });
    }

    protected virtual IEnumerable<SalesRepDocument> ApplyKeyword(IEnumerable<SalesRepDocument> documents, string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            return documents;
        }

        return documents.Where(x =>
            x.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true ||
            x.DisplayName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true);
    }

    protected virtual IEnumerable<SalesRepDocument> ApplySort(IEnumerable<SalesRepDocument> documents, IList<SortInfo> sortInfos)
    {
        if (sortInfos.IsNullOrEmpty())
        {
            return documents.OrderByDescending(x => x.CreatedDate);
        }

        IOrderedEnumerable<SalesRepDocument> ordered = null;

        foreach (var sortInfo in sortInfos)
        {
            var keySelector = GetSortKeySelector(sortInfo.SortColumn);
            var descending = sortInfo.SortDirection == SortDirection.Descending;

            ordered = ordered == null
                ? (descending ? documents.OrderByDescending(keySelector) : documents.OrderBy(keySelector))
                : (descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector));
        }

        return ordered;
    }

    protected virtual Func<SalesRepDocument, object> GetSortKeySelector(string sortColumn)
    {
        return sortColumn?.ToLowerInvariant() switch
        {
            "name" => x => x.DisplayName,
            "size" => x => x.Size,
            "modifieddate" => x => x.ModifiedDate,
            "ispinned" => x => x.IsPinned,
            _ => x => x.CreatedDate,
        };
    }
}
