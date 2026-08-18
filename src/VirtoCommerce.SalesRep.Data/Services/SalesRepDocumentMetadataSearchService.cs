using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDocumentMetadataSearchService(
        Func<ISalesRepRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        ISalesRepDocumentMetadataService crudService,
        IOptions<CrudOptions> crudOptions)
    : SearchService<SalesRepDocumentMetadataSearchCriteria, SalesRepDocumentMetadataSearchResult, SalesRepDocumentMetadata, DocumentMetadataEntity>(
        repositoryFactory,
        platformMemoryCache,
        crudService,
        crudOptions),
    ISalesRepDocumentMetadataSearchService
{
    protected override IQueryable<DocumentMetadataEntity> BuildQuery(IRepository repository, SalesRepDocumentMetadataSearchCriteria criteria)
    {
        var query = ((ISalesRepRepository)repository).DocumentMetadata;

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        // Category and keyword compare case-insensitively on every provider (PostgreSQL is case-sensitive by
        // default), matching the case-insensitive grouping of the category listing.
        if (!string.IsNullOrEmpty(criteria.Category))
        {
            var category = criteria.Category.ToLower();
            query = query.Where(x => x.Category.ToLower() == category);
        }

        if (criteria.IsPinned != null)
        {
            query = query.Where(x => x.IsPinned == criteria.IsPinned);
        }

        // The keyword matches the display name only — the raw file name is internal (download filename).
        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            var keyword = criteria.Keyword.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(keyword));
        }

        return query;
    }

    public virtual async Task<IList<SalesRepDocumentCategory>> GetCategoryCountsAsync(string keyword = null)
    {
        using var repository = repositoryFactory();

        var query = ((ISalesRepRepository)repository).DocumentMetadata.AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
        {
            var loweredKeyword = keyword.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(loweredKeyword));
        }

        // Case-insensitive grouping; each group is listed under its first (alphabetically smallest) stored spelling.
        var groups = await query
            .GroupBy(x => x.Category.ToLower())
            .Select(g => new { Name = g.Min(x => x.Category), Count = g.Count() })
            .OrderBy(x => x.Name)
            .ToListAsync();

        return groups
            .Select(group =>
            {
                var category = AbstractTypeFactory<SalesRepDocumentCategory>.TryCreateInstance();
                category.Name = group.Name;
                category.Count = group.Count;
                return category;
            })
            .ToList();
    }

    protected override IList<SortInfo> BuildSortExpression(SalesRepDocumentMetadataSearchCriteria criteria)
    {
        var sortInfos = criteria.SortInfos;

        if (sortInfos.IsNullOrEmpty())
        {
            sortInfos =
            [
                new SortInfo { SortColumn = nameof(DocumentMetadataEntity.IsPinned), SortDirection = SortDirection.Descending },
                new SortInfo { SortColumn = nameof(DocumentMetadataEntity.CreatedDate), SortDirection = SortDirection.Descending },
            ];
        }

        return sortInfos;
    }
}
