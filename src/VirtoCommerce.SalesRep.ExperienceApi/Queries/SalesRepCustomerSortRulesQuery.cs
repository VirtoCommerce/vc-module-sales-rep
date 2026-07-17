using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Returns the selectable "My customers" list orderings (VCST-5309). The selected rule's <c>name</c> is sent back as
/// the <c>salesRepCustomers</c> "sort" argument. Store configuration, not rep-scoped data.
/// </summary>
public class SalesRepCustomerSortRulesQuery : Query<IList<SalesRepCustomerSortRule>>
{
    /// <summary>Store to read the configured orderings from.</summary>
    public string StoreId { get; set; }

    /// <summary>Culture for localized sort labels (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to read the configured orderings from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized sort labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
