using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentCategoriesQuery : Query<IList<SalesRepDocumentCategory>>
{
    public string Keyword { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(Keyword), "Counts are computed over the keyword-filtered documents; zero-count categories are omitted.");
    }

    public override void Map(IResolveFieldContext context)
    {
        Keyword = context.GetArgument<string>(nameof(Keyword));
    }
}
