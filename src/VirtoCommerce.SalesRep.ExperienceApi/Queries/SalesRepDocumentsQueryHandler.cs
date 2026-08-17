using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentsQueryHandler : IQueryHandler<SalesRepDocumentsQuery, SalesRepDocumentSearchResult>
{
    private readonly ISalesRepDocumentSearchService _documentSearchService;

    public SalesRepDocumentsQueryHandler(ISalesRepDocumentSearchService documentSearchService)
    {
        _documentSearchService = documentSearchService;
    }

    public virtual Task<SalesRepDocumentSearchResult> Handle(SalesRepDocumentsQuery request, CancellationToken cancellationToken)
    {
        // No sort argument → empty criteria.Sort → the search service's isPinned:desc;createdDate:desc default.
        var criteria = request.GetSearchCriteria<SalesRepDocumentSearchCriteria>();
        criteria.Category = request.Category;
        criteria.IsPinned = request.Pinned;

        return _documentSearchService.SearchAsync(criteria);
    }
}
