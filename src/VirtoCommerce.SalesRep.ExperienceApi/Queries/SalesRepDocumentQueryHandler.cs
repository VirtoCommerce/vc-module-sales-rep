using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentQueryHandler : IQueryHandler<SalesRepDocumentQuery, SalesRepDocument>
{
    private readonly ISalesRepDocumentService _documentService;

    public SalesRepDocumentQueryHandler(ISalesRepDocumentService documentService)
    {
        _documentService = documentService;
    }

    public virtual Task<SalesRepDocument> Handle(SalesRepDocumentQuery request, CancellationToken cancellationToken)
    {
        return _documentService.GetAsync(request.Id);
    }
}
