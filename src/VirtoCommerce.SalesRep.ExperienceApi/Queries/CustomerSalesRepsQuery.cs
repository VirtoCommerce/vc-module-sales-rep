using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the Sales Reps that support the caller's organization (VCST-4907).
/// The organization is taken from the caller's identity, not from arguments.
/// </summary>
public class CustomerSalesRepsQuery : SearchQuery<SalesRepContactSearchResult>, IHasIncludeFields
{
    /// <summary>Organization the reps are resolved for (set server-side from the current user's claims).</summary>
    public string OrganizationId { get; set; }

    /// <summary>
    /// Optional store to scope the reps to. A Sales Rep's account is store-bound, so passing the storefront's
    /// current store keeps a rep from another store out of the results.
    /// </summary>
    public string StoreId { get; set; }

    /// <summary>GraphQL selection paths of the requested fields — drives the member response group (load only what was asked for).</summary>
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

        // Requested field paths (e.g. "items.emails", "items.phones") → used to load only the needed member data.
        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
