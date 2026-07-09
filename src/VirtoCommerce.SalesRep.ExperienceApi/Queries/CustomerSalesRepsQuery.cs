using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the Sales Reps that support the caller's organization (VCST-4907).
/// The organization is taken from the caller's identity, not from arguments.
/// </summary>
public class CustomerSalesRepsQuery : SearchQuery<SalesRepContactSearchResult>
{
    /// <summary>Organization the reps are resolved for (set server-side from the current user's claims).</summary>
    public string OrganizationId { get; set; }

    /// <summary>
    /// Optional store to scope the reps to. A Sales Rep's account is store-bound, so passing the storefront's
    /// current store keeps a rep from another store out of the results.
    /// </summary>
    public string StoreId { get; set; }

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
    }
}
