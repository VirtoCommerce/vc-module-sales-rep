using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentCategoriesQueryBuilder : SalesRepQueryBuilder<SalesRepDocumentCategoriesQuery, IList<SalesRepDocumentCategory>, ListGraphType<SalesRepDocumentCategoryType>>
{
    protected override string Name => "salesRepDocumentCategories";

    public SalesRepDocumentCategoriesQueryBuilder(IAuthorizationService authorizationService)
        : base(authorizationService)
    {
    }

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, SalesRepDocumentCategoriesQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        context.EnsureCanReadDocuments();
    }
}
