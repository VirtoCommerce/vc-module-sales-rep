using System;
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
/// Paged orders of a single customer organization the current Sales Rep is responsible for (VCST-5308) —
/// the "Orders" section of the customer profile. Supports keyword search, sorting and paging like the
/// storefront <c>orders</c> query, but is scoped to one customer and secured to the calling rep.
/// The Sales Rep is the caller; their security account id is set server-side from the caller's claims.
/// </summary>
public class SalesRepOrdersQuery : SearchQuery<SalesRepOrderSearchResult>, IHasIncludeFields
{
    /// <summary>Customer (organization) id whose orders to load.</summary>
    public string CustomerId { get; set; }

    /// <summary>Optional store to scope the orders to (the storefront's current store).</summary>
    public string StoreId { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    /// <summary>GraphQL selection paths of the requested fields — drives the order response group (load only what was asked for).</summary>
    public IList<string> IncludeFields { get; set; } = [];

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(CustomerId), "Customer (organization) id whose orders to load.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (defaults to all stores).");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        // Identity comes from the caller's claims; only the customer id (and optional store) are client arguments.
        CustomerId = context.GetArgument<string>(nameof(CustomerId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        UserId = context.GetCurrentUserId();

        // Requested field paths (e.g. "items.total", "items.itemsCount") → used to load only the needed order data.
        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
