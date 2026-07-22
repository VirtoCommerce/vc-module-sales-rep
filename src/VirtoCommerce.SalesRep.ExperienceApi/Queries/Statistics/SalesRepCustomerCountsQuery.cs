using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerCountsQuery : Query<SalesRepCustomerCountsContext>
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id to scope to; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders behind the counters to (defaults to all stores).");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        UserId = context.GetCurrentUserId();
    }
}
