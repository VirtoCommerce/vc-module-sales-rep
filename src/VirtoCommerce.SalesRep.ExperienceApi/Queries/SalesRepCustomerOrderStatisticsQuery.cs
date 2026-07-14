using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Order statistics for the current Sales Rep's customers (VCST-5309). Standalone — decoupled from the
/// customer-details card so the sales-performance widgets can evolve on their own. The Sales Rep is the caller;
/// their security account id is set server-side from the claims. When a <see cref="CustomerId"/> is given the
/// handler verifies the caller serves it; when omitted, the statistics span every organization the rep is
/// assigned to (the combined cross-customer view).
/// </summary>
public class SalesRepCustomerOrderStatisticsQuery : Query<CustomerOrderStatisticsContext>
{
    /// <summary>
    /// Customer (organization) id whose orders are aggregated. Omit for a cross-customer view — the combined
    /// statistics of every organization the rep is assigned to.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>Optional store to scope the orders to (defaults to all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency to convert all figures to (defaults to the store's default currency, then the primary currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(CustomerId), "Customer (organization) id whose orders to aggregate; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (defaults to all stores).");
        yield return Argument<StringGraphType>(nameof(CurrencyCode), "Currency to convert all figures to (defaults to the store's default currency).");
    }

    public override void Map(IResolveFieldContext context)
    {
        // Identity comes from the caller's claims; only the customer/store/currency are client arguments.
        CustomerId = context.GetArgument<string>(nameof(CustomerId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        UserId = context.GetCurrentUserId();
    }
}
