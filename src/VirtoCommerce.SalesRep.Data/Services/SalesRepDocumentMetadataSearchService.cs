using System;
using System.Collections.Generic;
using System.Linq;
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
