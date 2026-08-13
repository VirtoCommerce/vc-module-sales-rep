using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
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

        context.EnsureCanReadDocuments();
    }
}
