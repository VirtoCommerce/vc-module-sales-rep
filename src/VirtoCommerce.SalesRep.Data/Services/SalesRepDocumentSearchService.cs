using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Search orchestrator. The category/isPinned/objectIds predicates run DB-side through the metadata SearchService;
// the joined AssetEntries come from the Assets CRUD cache. Keyword (matches the file name OR the display name) and
// the final sort span both tables, so they run in memory over the small library, then paging is applied in memory.
// No custom cache region: the metadata Crud/Search regions and the AssetEntry regions each expire on their own
// mutations, so composing their cached reads stays correct without manual invalidation.
public class SalesRepDocumentSearchService : ISalesRepDocumentSearchService
{
    private const int MetadataPageSize = 100;

    private readonly ISalesRepDocumentMetadataSearchService _metadataSearchService;
    private readonly IAssetEntryService _assetEntryService;

    public SalesRepDocumentSearchService(
        ISalesRepDocumentMetadataSearchService metadataSearchService,
        IAssetEntryService assetEntryService)
    {
        _metadataSearchService = metadataSearchService;
        _assetEntryService = assetEntryService;
    }

    public virtual async Task<SalesRepDocumentSearchResult> SearchAsync(SalesRepDocumentSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var documents = await GetDocumentsAsync(criteria.Category, criteria.IsPinned, criteria.ObjectIds);

        var matched = ApplySort(ApplyKeyword(documents, criteria.Keyword), criteria.SortInfos).ToList();

        var result = AbstractTypeFactory<SalesRepDocumentSearchResult>.TryCreateInstance();
        result.TotalCount = matched.Count;
        result.Results = matched
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToList();

        return result;
    }

    public virtual async Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync(string keyword = null)
    {
        var documents = ApplyKeyword(await GetDocumentsAsync(category: null, isPinned: null, objectIds: null), keyword);

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

    // The metadata rows (1:1 with a library AssetEntry, created on upload) are the library's source of truth. Their
    // AssetEntries are fetched from the Assets CRUD cache and joined; a foreign or missing Group entry is dropped.
    protected virtual async Task<IList<SalesRepDocument>> GetDocumentsAsync(string category, bool? isPinned, IList<string> objectIds)
    {
        var metadataCriteria = AbstractTypeFactory<SalesRepDocumentMetadataSearchCriteria>.TryCreateInstance();
        metadataCriteria.Category = category;
        metadataCriteria.IsPinned = isPinned;
        metadataCriteria.ObjectIds = objectIds;
        metadataCriteria.Take = MetadataPageSize;

        var metadata = await _metadataSearchService.SearchAllNoCloneAsync(metadataCriteria);

        if (metadata.Count == 0)
        {
            return [];
        }

        var entriesById = (await _assetEntryService.GetAsync(metadata.Select(x => x.Id).ToList(), clone: false))
            .Where(x => ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Group))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadata
            .Where(x => entriesById.ContainsKey(x.Id))
            .Select(x => SalesRepDocumentMapper.ToModel(entriesById[x.Id], x))
            .ToList();
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
            // Pinned floats to the top, newest first within each group (isPinned:desc;createdDate:desc).
            return documents
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.CreatedDate);
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
