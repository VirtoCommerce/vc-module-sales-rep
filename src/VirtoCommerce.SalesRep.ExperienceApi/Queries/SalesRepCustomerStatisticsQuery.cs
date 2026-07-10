using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for a single customer organization's order statistics (VCST-5309). Standalone — decoupled from the
/// customer-details card so the sales-performance widgets can evolve on their own. The Sales Rep is the caller;
/// their security account id is set server-side from the claims, and the handler verifies the caller actually
/// serves the requested organization.
/// </summary>
public class SalesRepCustomerStatisticsQuery : Query<CustomerOrderStatisticsContext>
{
    /// <summary>Organization (customer) id whose orders are aggregated.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the orders to (defaults to all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency to convert all figures to (defaults to the store's default currency, then the primary currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>("id", "Organization (customer) id.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (defaults to all stores).");
        yield return Argument<StringGraphType>(nameof(CurrencyCode), "Currency to convert all figures to (defaults to the store's default currency).");
    }

    public override void Map(IResolveFieldContext context)
    {
        // Identity comes from the caller's claims; only the organization/store/currency are client arguments.
        OrganizationId = context.GetArgument<string>("id");
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        UserId = context.GetCurrentUserId();
    }
}
