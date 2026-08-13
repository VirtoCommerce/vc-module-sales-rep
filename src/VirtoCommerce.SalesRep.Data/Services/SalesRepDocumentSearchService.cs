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
// categories/counts derived in memory, and the whole set cached (TTL setting + expired on module mutations).
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

        var entries = (await GetAllEntriesAsync()).AsEnumerable();

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            entries = entries.Where(x => criteria.ObjectIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(criteria.Category))
        {
            entries = entries.Where(x => criteria.Category.EqualsIgnoreCase(SalesRepDocumentMapper.GetCategory(x.BlobInfo?.RelativeUrl)));
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            entries = entries.Where(x => x.BlobInfo?.Name?.Contains(criteria.Keyword, StringComparison.OrdinalIgnoreCase) == true);
        }

        var matched = ApplySort(entries, criteria.SortInfos).ToList();

        var result = AbstractTypeFactory<SalesRepDocumentSearchResult>.TryCreateInstance();
        result.TotalCount = matched.Count;

        var page = matched.Skip(criteria.Skip).Take(criteria.Take).ToList();
        var metadataById = (await _metadataService.GetByIdsAsync(page.Select(x => x.Id).ToList()))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        result.Results = page
            .Select(x => SalesRepDocumentMapper.ToModel(x, metadataById.GetValueOrDefault(x.Id)))
            .ToList();

        return result;
    }

    public virtual async Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync()
    {
        var entries = await GetAllEntriesAsync();

        return entries
            .Select(x => SalesRepDocumentMapper.GetCategory(x.BlobInfo?.RelativeUrl))
            .Where(x => !string.IsNullOrEmpty(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
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

    protected virtual Task<IList<AssetEntry>> GetAllEntriesAsync()
    {
        var cacheKey = CacheKey.With(GetType(), nameof(GetAllEntriesAsync));

        return _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async options =>
        {
            options.AddExpirationToken(SalesRepDocumentCacheRegion.CreateChangeToken());

            var minutes = await _settingsManager.GetValueAsync<int>(ModuleConstants.Settings.Caching.DocumentsCacheExpiration);
            options.AbsoluteExpirationRelativeToNow = minutes > 0 ? TimeSpan.FromMinutes(minutes) : CacheDisabled;

            var criteria = AbstractTypeFactory<AssetEntrySearchCriteria>.TryCreateInstance();
            criteria.Group = ModuleConstants.DocumentsScope;
            criteria.Take = SearchAllPageSize;
            criteria.Sort = "createdDate:desc"; // the search-all loop requires an explicit sort

            return await _assetEntrySearchService.SearchAllAsync(criteria);
        });
    }

    protected virtual IEnumerable<AssetEntry> ApplySort(IEnumerable<AssetEntry> entries, IList<SortInfo> sortInfos)
    {
        if (sortInfos.IsNullOrEmpty())
        {
            return entries.OrderByDescending(x => x.CreatedDate);
        }

        IOrderedEnumerable<AssetEntry> ordered = null;

        foreach (var sortInfo in sortInfos)
        {
            var keySelector = GetSortKeySelector(sortInfo.SortColumn);
            var descending = sortInfo.SortDirection == SortDirection.Descending;

            ordered = ordered == null
                ? (descending ? entries.OrderByDescending(keySelector) : entries.OrderBy(keySelector))
                : (descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector));
        }

        return ordered;
    }

    protected virtual Func<AssetEntry, object> GetSortKeySelector(string sortColumn)
    {
        return sortColumn?.ToLowerInvariant() switch
        {
            "name" => x => x.BlobInfo?.Name,
            "size" => x => x.BlobInfo?.Size ?? 0,
            "modifieddate" => x => x.ModifiedDate,
            _ => x => x.CreatedDate,
        };
    }
}
