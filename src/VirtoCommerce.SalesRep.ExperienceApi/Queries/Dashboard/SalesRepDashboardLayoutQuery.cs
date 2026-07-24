using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Dashboard;

public class SalesRepDashboardLayoutQuery : Query<DashboardLayout>
{
    public string Scope { get; set; }

    public string StoreId { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(Scope), "Layout surface identifier (e.g. \"dashboard\", \"customerProfile\").");
        yield return Argument<StringGraphType>(nameof(StoreId), "Optional store the layout is scoped to.");
    }

    public override void Map(IResolveFieldContext context)
    {
        Scope = context.GetArgument<string>(nameof(Scope));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        UserId = context.GetCurrentUserId();
    }
}
