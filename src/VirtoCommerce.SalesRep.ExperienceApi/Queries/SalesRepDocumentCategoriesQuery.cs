using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepDocumentCategoriesQuery : Query<IList<SalesRepDocumentCategory>>
{
    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield break;
    }

    public override void Map(IResolveFieldContext context)
    {
    }
}
