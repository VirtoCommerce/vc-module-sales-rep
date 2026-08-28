using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepActivitiesQuery : Query<SalesRepActivitySearchResult>
{
    public const int DefaultTake = SalesRepActivitySearchCriteria.DefaultTake;

    public const int MaxTake = 50;

    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public IList<string> Categories { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public int Take { get; set; } = DefaultTake;

    public int Skip { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id to scope to; omit for all the rep's assigned customers (\"my activity\").");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the activity to (defaults to all stores).");
        yield return Argument<ListGraphType<NonNullGraphType<StringGraphType>>>(nameof(Categories), "Activity categories to include (orders, customers, searches, productViews, logins); omit for all.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Optional date range to scope the activity to (omit for all dates).");
        yield return Argument<IntGraphType>(nameof(Take), $"How many activity rows to return (default {DefaultTake}, max {MaxTake}; zero or less returns only counts).");
        yield return Argument<IntGraphType>(nameof(Skip), $"How many activity rows to skip (default 0). The feed pages {ModuleConstants.Activities.MaxSkip} rows deep; beyond that it returns no rows, while the counters keep reporting the whole set.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the localized and money fields (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Categories = context.GetArgument<IList<string>>(nameof(Categories));
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        Take = context.GetArgument<int?>(nameof(Take)) ?? DefaultTake;
        Skip = context.GetArgument<int?>(nameof(Skip)) ?? 0;
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
