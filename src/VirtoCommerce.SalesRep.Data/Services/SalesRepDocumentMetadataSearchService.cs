using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Caching;
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
    private readonly IPlatformMemoryCache _platformMemoryCache = platformMemoryCache;

    protected override IQueryable<DocumentMetadataEntity> BuildQuery(IRepository repository, SalesRepDocumentMetadataSearchCriteria criteria)
    {
        var query = ((ISalesRepRepository)repository).DocumentMetadata;

        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            query = query.Where(x => criteria.ObjectIds.Contains(x.Id));
        }

        // Case-insensitivity is a DB concern: SqlServer/MySql default collations are CI; PostgreSQL gets the
        // case_insensitive collation on Name and Category via Data.PostgreSql's DocumentMetadataEntityConfiguration.
        if (!string.IsNullOrEmpty(criteria.Category))
        {
            query = query.Where(x => x.Category == criteria.Category);
        }

        if (criteria.IsPinned != null)
        {
            query = query.Where(x => x.IsPinned == criteria.IsPinned);
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            query = query.Where(x => x.Name.Contains(criteria.Keyword));
        }

        return query;
    }

    public virtual Task<IList<SalesRepDocumentCategory>> GetCategoryCountsAsync(string keyword = null)
    {
        var cacheKey = CacheKey.With(GetType(), nameof(GetCategoryCountsAsync), keyword);

        return _platformMemoryCache.GetOrCreateExclusiveAsync(cacheKey, async cacheEntry =>
        {
            cacheEntry.AddExpirationToken(GenericSearchCachingRegion<SalesRepDocumentMetadata>.CreateChangeToken());

            var criteria = AbstractTypeFactory<SalesRepDocumentMetadataSearchCriteria>.TryCreateInstance();
            criteria.Keyword = keyword;

            using var repository = repositoryFactory();

            var groups = await BuildQuery(repository, criteria)
                .GroupBy(x => x.Category)
                .Select(g => new { Name = g.Key, Count = g.Count() })
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
                .ToList() as IList<SalesRepDocumentCategory>;
        });
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
