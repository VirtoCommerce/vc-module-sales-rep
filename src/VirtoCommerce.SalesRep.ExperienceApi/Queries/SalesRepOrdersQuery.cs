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
    /// <summary>
    /// Organization (customer) id whose orders to load. Omit for a cross-customer dashboard — the orders of every
    /// organization the rep is assigned to.
    /// </summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the orders to (the storefront's current store).</summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Optional selected statuses (each a <c>SalesRepOrderStatus.name</c>). The handler resolves each to its
    /// underlying order statuses (1:many for composite/overridden statuses) and filters by their union; omit (or
    /// empty) for no status filter. A list so multi-select works without a future breaking change — single-select
    /// is a one-element list.
    /// </summary>
    public IList<string> Statuses { get; set; }

    /// <summary>
    /// Culture for the localized <c>statusDisplayValue</c> field (e.g. "en-US"). Consumed by the
    /// <c>SalesRepOrderType</c> LocalizedField resolver via the request context (the builder copies it to the
    /// UserContext), not by this handler.
    /// </summary>
    public string CultureName { get; set; }

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

        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id whose orders to load; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (defaults to all stores).");
        yield return Argument<ListGraphType<StringGraphType>>(nameof(Statuses), "Selected statuses (salesRepOrderStatuses 'name's); filters to the union of their underlying order statuses.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the localized statusDisplayValue field (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        // Identity comes from the caller's claims; only the customer id (and optional store) are client arguments.
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Statuses = context.GetArgument<string[]>(nameof(Statuses));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();

        // Requested field paths (e.g. "items.total", "items.itemsCount") → used to load only the needed order data.
        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
