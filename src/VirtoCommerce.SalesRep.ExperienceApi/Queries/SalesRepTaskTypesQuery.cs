using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskTypesQuery : Query<IList<string>>
{
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield break;
    }

    public override void Map(IResolveFieldContext context)
    {
        UserId = context.GetCurrentUserId();
    }
}
