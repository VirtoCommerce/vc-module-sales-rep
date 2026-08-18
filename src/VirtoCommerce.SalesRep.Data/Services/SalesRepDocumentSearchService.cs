using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Services;

// Every filter, sort and keyword field lives on the metadata row (the display name is always stored, the raw
// file name is internal), so the search is a single DB-paged metadata query; only the returned page's files
// are fetched. No custom cache region: the metadata Crud/Search regions expire on their own mutations.
public class SalesRepDocumentSearchService : ISalesRepDocumentSearchService
{
    private readonly ISalesRepDocumentMetadataSearchService _metadataSearchService;
    private readonly IFileUploadService _fileUploadService;

    public SalesRepDocumentSearchService(
        ISalesRepDocumentMetadataSearchService metadataSearchService,
        IFileUploadService fileUploadService)
    {
        _metadataSearchService = metadataSearchService;
        _fileUploadService = fileUploadService;
    }

    public virtual async Task<SalesRepDocumentSearchResult> SearchAsync(SalesRepDocumentSearchCriteria criteria, bool clone = true)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var metadataResult = await _metadataSearchService.SearchNoCloneAsync(ToMetadataCriteria(criteria));

        var result = AbstractTypeFactory<SalesRepDocumentSearchResult>.TryCreateInstance();
        result.TotalCount = metadataResult.TotalCount;
        result.Results = await MapToDocumentsAsync(metadataResult.Results);

        return result;
    }

    public virtual Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync(string keyword = null)
    {
        return _metadataSearchService.GetCategoryCountsAsync(keyword);
    }

    protected virtual SalesRepDocumentMetadataSearchCriteria ToMetadataCriteria(SalesRepDocumentSearchCriteria criteria)
    {
        var metadataCriteria = AbstractTypeFactory<SalesRepDocumentMetadataSearchCriteria>.TryCreateInstance();

        metadataCriteria.ObjectIds = criteria.ObjectIds;
        metadataCriteria.Category = criteria.Category;
        metadataCriteria.IsPinned = criteria.IsPinned;
        metadataCriteria.Keyword = criteria.Keyword;
        metadataCriteria.Skip = criteria.Skip;
        metadataCriteria.Take = criteria.Take;
        metadataCriteria.Sort = SortInfo.ToString(MapSortInfos(criteria.SortInfos));

        return metadataCriteria;
    }

    // Maps the public sort tokens to metadata columns ("name" = the display name); unknown tokens are dropped,
    // falling back to the default pinned-first, newest-first ordering.
    protected virtual IList<SortInfo> MapSortInfos(IList<SortInfo> sortInfos)
    {
        return sortInfos
            .Select(sortInfo => new SortInfo
            {
                SortColumn = MapSortColumn(sortInfo.SortColumn),
                SortDirection = sortInfo.SortDirection,
            })
            .Where(x => x.SortColumn != null)
            .ToList();
    }

    protected virtual string MapSortColumn(string sortColumn)
    {
        return sortColumn?.ToLowerInvariant() switch
        {
            "name" => nameof(DocumentMetadataEntity.Name),
            "ispinned" => nameof(DocumentMetadataEntity.IsPinned),
            "createddate" => nameof(DocumentMetadataEntity.CreatedDate),
            "modifieddate" => nameof(DocumentMetadataEntity.ModifiedDate),
            _ => null,
        };
    }

    protected virtual async Task<IList<SalesRepDocument>> MapToDocumentsAsync(IList<SalesRepDocumentMetadata> metadatas)
    {
        if (metadatas.Count == 0)
        {
            return [];
        }

        var filesById = (await _fileUploadService.GetAsync(metadatas.Select(x => x.FileId).ToList(), clone: false))
            .Where(x => ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadatas
            .Where(x => filesById.ContainsKey(x.FileId))
            .Select(x => SalesRepDocumentMapper.ToModel(filesById[x.FileId], x))
            .ToList();
    }
}
