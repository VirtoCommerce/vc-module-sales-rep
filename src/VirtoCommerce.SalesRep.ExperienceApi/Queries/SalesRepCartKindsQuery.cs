using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Returns the selectable Sales Rep cart kinds. The selected kind's <c>name</c> is sent back in the
/// <c>salesRepCustomerCartStatistics</c> <c>filters</c> argument. Caller-agnostic — kinds are store configuration,
/// not rep-scoped data. Mirrors <see cref="SalesRepOrderStatusesQuery"/>.
/// </summary>
public class SalesRepCartKindsQuery : Query<IList<SalesRepCartKind>>
{
    /// <summary>Store to read the configured cart kinds from.</summary>
    public string StoreId { get; set; }

    /// <summary>Culture for localized kind labels (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to read the configured cart kinds from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized kind labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
