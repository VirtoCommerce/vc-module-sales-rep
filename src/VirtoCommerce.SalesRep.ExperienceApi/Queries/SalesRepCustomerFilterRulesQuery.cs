using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Returns the selectable Sales Rep customer segments. The selected segment's <c>name</c> is sent back in the
/// <c>salesRepCustomers</c> / <c>salesRepCustomerCounts</c> <c>filter</c> argument. Caller-agnostic — segments are
/// store/project configuration, not rep-scoped data. Empty by default (see <c>SalesRepCustomerFilterRuleResolver</c>).
/// </summary>
public class SalesRepCustomerFilterRulesQuery : Query<IList<SalesRepCustomerFilterRule>>
{
    /// <summary>Store to read the configured customer segments from.</summary>
    public string StoreId { get; set; }

    /// <summary>Culture for localized segment labels (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to read the configured customer segments from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized segment labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
