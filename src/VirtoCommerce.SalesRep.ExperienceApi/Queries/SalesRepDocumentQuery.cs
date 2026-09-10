using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentQuery : Query<SalesRepDocument>
{
    public string Id { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Id), "Document id.");
    }

    public override void Map(IResolveFieldContext context)
    {
        Id = context.GetArgument<string>(nameof(Id));
    }
}
