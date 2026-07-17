using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Returns the selectable "Top Sellers" category badges (VCST-5309) — the store catalog's top-level non-hidden
/// categories. The selected rule's <c>name</c> (a category id) is sent back as the <c>salesRepTopSellers</c>
/// "filter" argument.
/// </summary>
public class SalesRepTopSellerFilterRulesQuery : Query<IList<SalesRepTopSellerFilterRule>>
{
    /// <summary>Store whose catalog's top-level categories are returned.</summary>
    public string StoreId { get; set; }

    /// <summary>Culture for localized category labels (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store whose catalog's top-level categories are returned.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized category labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
    }
}
