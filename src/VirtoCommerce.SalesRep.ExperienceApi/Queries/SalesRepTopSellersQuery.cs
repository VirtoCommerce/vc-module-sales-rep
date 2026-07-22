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

public class SalesRepTopSellersQuery : Query<IList<SalesRepTopSeller>>
{
    public const int DefaultTake = SalesRepTopSellerCriteria.DefaultTake;

    public const int MaxTake = 10;

    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public string Filter { get; set; }

    public string Sort { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public int Take { get; set; } = DefaultTake;

    public string CurrencyCode { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id to scope to; omit for all the rep's assigned customers.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (and whose catalog backs the category filter).");
        yield return Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Selected category badge (a salesRepTopSellerFilterRules 'name'); restricts to that category's subtree. Omit for all categories.");
        yield return Argument<StringGraphType>(nameof(Sort), "Selected ordering (a salesRepTopSellerSortRules 'name'); defaults to by-units.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Optional created-date range to aggregate over (omit for lifetime).");
        yield return Argument<IntGraphType>(nameof(Take), $"How many top products to return (default {DefaultTake}, max {MaxTake}).");
        yield return Argument<StringGraphType>(nameof(CurrencyCode), "Currency to convert revenue to (defaults to the store's default currency).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the revenue formatted amount (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Filter = context.GetArgument<string>(SalesRepFilters.ArgumentName);
        Sort = context.GetArgument<string>(nameof(Sort));
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        Take = context.GetArgument<int?>(nameof(Take)) ?? DefaultTake;
        CurrencyCode = context.GetArgument<string>(nameof(CurrencyCode));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
