using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTasksQuery : SearchQuery<SalesRepTaskSearchResult>
{
    public string StoreId { get; set; }

    public string Filter { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public DateTime? Today { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

    public string MemberId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the tasks to (defaults to all stores).");
        yield return Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Selected filter-rule name (a salesRepTaskFilterRules 'name'). Omit for all the caller's tasks.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Optional due-date window, e.g. the month a calendar is showing (omit for all dates). Intersects with the selected filter rule rather than replacing it.");
        yield return Argument<DateTimeGraphType>(nameof(Today), "Start of the caller's current day as an instant, e.g. 2026-05-28T05:00:00Z for a UTC-5 viewer. Decides where 'upcoming' ends and 'overdue' begins; send the same boundary used to render the status pills, or the tabs and the pills will disagree. Defaults to the start of the current UTC day.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized labels (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        StoreId = context.GetArgument<string>(nameof(StoreId));
        Filter = context.GetArgument<string>(SalesRepFilters.ArgumentName);
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        Today = context.GetArgument<DateTime?>(nameof(Today));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
        MemberId = context.GetCurrentMemberId();
    }
}
