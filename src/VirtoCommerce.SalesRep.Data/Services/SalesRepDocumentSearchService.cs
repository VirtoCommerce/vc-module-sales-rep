using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Services;

// A single DB-paged metadata query (every search field lives on the metadata row); only the page's files are fetched.
public class SalesRepDocumentSearchService : ISalesRepDocumentSearchService
{
    private readonly ISalesRepDocumentMetadataSearchService _metadataSearchService;
    private readonly IFileUploadService _fileUploadService;
    private readonly ISalesRepMapper _mapper;

    public SalesRepDocumentSearchService(
        ISalesRepDocumentMetadataSearchService metadataSearchService,
        IFileUploadService fileUploadService,
        ISalesRepMapper mapper)
    {
        _metadataSearchService = metadataSearchService;
        _fileUploadService = fileUploadService;
        _mapper = mapper;
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

    // Unknown sort tokens are dropped — the default pinned-first, newest-first ordering applies.
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

    protected virtual async Task<IList<SalesRepDocument>> MapToDocumentsAsync(IList<SalesRepDocumentMetadata> metadataItems)
    {
        if (metadataItems.Count == 0)
        {
            return [];
        }

        var files = await _fileUploadService.GetAsync(metadataItems.Select(x => x.FileId).ToList(), clone: false);

        return _mapper.ToDocuments(files, metadataItems);
    }
}
