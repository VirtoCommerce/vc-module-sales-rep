using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// The rep's top-selling products (VCST-5309, dashboard + customer "Top Sellers"). Ranks the rep's own order line
/// items grouped by product over an optional period; pass <c>organizationId</c> to scope to one customer, omit it
/// for all assigned customers. Ordering is a <c>salesRepTopSellerSortRules</c> name (default by-units); an optional
/// category badge (<c>filter</c>, a <c>salesRepTopSellerFilterRules</c> name) restricts to a category subtree. The
/// Sales Rep is the caller; their security account id is set server-side from the claims.
/// </summary>
public class SalesRepTopSellersQuery : Query<IList<SalesRepTopSeller>>
{
    /// <summary>Organization (customer) id to scope to; omit for all the rep's assigned customers.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Optional store to scope the orders to (and whose catalog backs the category filter).</summary>
    public string StoreId { get; set; }

    /// <summary>Selected category badge (a <c>SalesRepTopSellerFilterRule.name</c>); omit for all categories. Unrecognized → no results (fail-closed).</summary>
    public string Filter { get; set; }

    /// <summary>Selected ordering (a <c>SalesRepTopSellerSortRule.name</c>); empty/unknown → by-units.</summary>
    public string Sort { get; set; }

    /// <summary>Optional created-date range to aggregate over. Omit for lifetime.</summary>
    public SalesRepStatisticsPeriodInput Period { get; set; }

    /// <summary>How many top products to return (default 5, clamped to a max of 10).</summary>
    public int Take { get; set; } = 5;

    /// <summary>Currency to convert revenue to (defaults to the store's default currency, then the primary currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Culture for the revenue formatted amount (e.g. "en-US"); consumed by the MoneyType resolver via the UserContext.</summary>
    public string CultureName { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id to scope to; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (and whose catalog backs the category filter).");
        yield return Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Selected category badge (a salesRepTopSellerFilterRules 'name'); restricts to that category's subtree. Omit for all categories.");
        yield return Argument<StringGraphType>("sort", "Selected ordering (a salesRepTopSellerSortRules 'name'); defaults to by-units.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Optional created-date range to aggregate over (omit for lifetime).");
        yield return Argument<IntGraphType>(nameof(Take), "How many top products to return (default 5, max 10).");
        yield return Argument<StringGraphType>(nameof(CurrencyCode), "Currency to convert revenue to (defaults to the store's default currency).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the revenue formatted amount (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Filter = context.GetArgument<string>(SalesRepFilters.ArgumentName);
        Sort = context.GetArgument<string>("sort");
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        Take = context.GetArgument<int?>(nameof(Take)) ?? 5;
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
