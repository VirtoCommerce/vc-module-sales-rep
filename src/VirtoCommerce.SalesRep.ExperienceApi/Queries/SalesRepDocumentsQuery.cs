using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentsQuery : SearchQuery<SalesRepDocumentSearchResult>
{
    public string Category { get; set; }

    public bool? Pinned { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(Category), "Category to filter by — a salesRepDocumentCategories 'name'. Omit for all categories.");
        yield return Argument<BooleanGraphType>(nameof(Pinned), "Pinned-flag filter: true returns only the pinned document, false only unpinned ones. Omit for all.");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        Category = context.GetArgument<string>(nameof(Category));
        Pinned = context.GetArgument<bool?>(nameof(Pinned));
    }
}
