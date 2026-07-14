using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Returns the order-status tabs for the Sales Rep orders panel (VCST-5308). The selected tab's <c>name</c> is
/// sent back as the <c>salesRepOrders</c> "status" argument. Caller-agnostic — statuses are store configuration,
/// not rep-scoped data.
/// </summary>
public class SalesRepOrderStatusesQuery : Query<SalesRepOrderStatusesResult>
{
    /// <summary>Store to read the configured order statuses from.</summary>
    public string StoreId { get; set; }

    /// <summary>Culture for localized status labels (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to read the configured order statuses from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized status labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
