using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentCategoriesQueryHandler : IQueryHandler<SalesRepDocumentCategoriesQuery, IList<SalesRepDocumentCategory>>
{
    private readonly ISalesRepDocumentSearchService _documentSearchService;

    public SalesRepDocumentCategoriesQueryHandler(ISalesRepDocumentSearchService documentSearchService)
    {
        _documentSearchService = documentSearchService;
    }

    public virtual Task<IList<SalesRepDocumentCategory>> Handle(SalesRepDocumentCategoriesQuery request, CancellationToken cancellationToken)
    {
        return _documentSearchService.GetCategoriesAsync(request.Keyword);
    }
}
