using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class CustomerSalesRepsQuery : SearchQuery<SalesRepContactSearchResult>, IHasIncludeFields
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public IList<string> IncludeFields { get; set; } = [];

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope reps to (their account's store; defaults to all stores).");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);
        OrganizationId = context.GetCurrentOrganizationId();
        StoreId = context.GetArgument<string>(nameof(StoreId));

        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
