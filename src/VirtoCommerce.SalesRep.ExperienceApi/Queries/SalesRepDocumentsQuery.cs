using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentsQuery : SearchQuery<SalesRepDocumentSearchResult>
{
    public string Category { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(Category), "Category (first-level subfolder) to filter by — a salesRepDocumentCategories 'name'. Omit for all categories.");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        Category = context.GetArgument<string>(nameof(Category));
    }
}
