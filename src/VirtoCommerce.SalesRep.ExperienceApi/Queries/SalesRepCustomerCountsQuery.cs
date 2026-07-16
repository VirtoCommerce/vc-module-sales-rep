using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// "My customers" counters for the current Sales Rep (dashboard "My Customers" widget): how many customers they
/// serve, how many ordered in a period, and how many are new in a period. Standalone; pass <see cref="OrganizationId"/>
/// to scope to a single customer, or omit it for the combined view. Secured to the calling rep and derived only from
/// orders the rep created.
/// </summary>
public class SalesRepCustomerCountsQuery : Query<SalesRepCustomerCountsContext>
{
    /// <summary>Organization (customer) id to scope to. Omit for all the rep's assigned customers.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the orders behind the counters to (defaults to all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
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
