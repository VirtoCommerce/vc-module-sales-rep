using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentsQueryBuilder : SalesRepSearchQueryBuilder<SalesRepDocumentsQuery, SalesRepDocumentSearchResult, SalesRepDocument, SalesRepDocumentType>
{
    protected override string Name => "salesRepDocuments";

    public SalesRepDocumentsQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepDocumentsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        await Authorize(context, request, new SalesRepDocumentAuthorizationRequirement(ModuleConstants.Security.Permissions.DocumentsRead));
    }
}
