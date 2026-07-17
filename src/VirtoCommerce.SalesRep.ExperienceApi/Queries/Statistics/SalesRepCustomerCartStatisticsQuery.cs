using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// Cart/project statistics for the current Sales Rep (dashboard "Active Projects" and related cart widgets).
/// Standalone, mirroring <see cref="SalesRepCustomerOrderStatisticsQuery"/>: pass <see cref="OrganizationId"/> to
/// scope to a single customer, or omit it for the combined view of every organization the rep serves. Secured to
/// the calling rep and limited to carts the rep created (their user id is the cart's CustomerId).
/// </summary>
public class SalesRepCustomerCartStatisticsQuery : Query<CustomerCartStatisticsContext>
{
    /// <summary>Organization (customer) id whose carts are aggregated. Omit for a cross-customer view.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the carts to (defaults to all stores).</summary>
    public string StoreId { get; set; }

    /// <summary>Currency to convert all figures to (defaults to the store's default currency, then the primary currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Culture for the money fields' formatted amounts (e.g. "en-US"), consumed by the MoneyType resolvers.</summary>
    public string CultureName { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id whose carts to aggregate; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the carts to (defaults to all stores).");
        yield return Argument<StringGraphType>(nameof(CurrencyCode), "Currency to convert all figures to (defaults to the store's default currency).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the money fields' formatted amounts (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
