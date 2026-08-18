using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Keyword and the final sort span both the metadata table and the file store, so they run (with paging) in memory over the small library.
// No custom cache region: the metadata Crud/Search regions and the underlying AssetEntry regions each expire on their own mutations, so composing their cached reads stays correct without manual invalidation.
public class SalesRepDocumentSearchService : ISalesRepDocumentSearchService
{
    private const int MetadataPageSize = 100;

    private readonly ISalesRepDocumentMetadataSearchService _metadataSearchService;
    private readonly IFileUploadService _fileUploadService;

    public SalesRepDocumentSearchService(
        ISalesRepDocumentMetadataSearchService metadataSearchService,
        IFileUploadService fileUploadService)
    {
        _metadataSearchService = metadataSearchService;
        _fileUploadService = fileUploadService;
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

        var filesById = (await _fileUploadService.GetAsync(metadata.Select(x => x.FileId).ToList(), clone: false))
            .Where(x => ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadata
            .Where(x => filesById.ContainsKey(x.FileId))
            .Select(x => SalesRepDocumentMapper.ToModel(filesById[x.FileId], x))
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
            return documents
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.CreatedDate);
        }

        IOrderedEnumerable<SalesRepDocument> ordered = null;

        foreach (var sortInfo in sortInfos)
        {
            var keySelector = GetSortKeySelector(sortInfo.SortColumn);
            var descending = sortInfo.SortDirection == SortDirection.Descending;

            if (ordered == null)
            {
                ordered = descending ? documents.OrderByDescending(keySelector) : documents.OrderBy(keySelector);
            }
            else
            {
                ordered = descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
            }
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
