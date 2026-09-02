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

    public string UserId { get; set; }

    public string MemberId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the tasks to (defaults to all stores).");
        yield return Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Filter-rule name from salesRepTaskFilterRules. Omit for all the caller's tasks; an unrecognized name returns nothing rather than the unfiltered list.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Due-date window. Intersects with the filter rule rather than replacing it.");
        yield return Argument<DateTimeGraphType>(nameof(Today), "Start of the caller's local day, which is where 'overdue' ends and 'upcoming' begins. Defaults to the current UTC day.");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        StoreId = context.GetArgument<string>(nameof(StoreId));
        Filter = context.GetArgument<string>(SalesRepFilters.ArgumentName);
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        Today = context.GetArgument<DateTime?>(nameof(Today));
        UserId = context.GetCurrentUserId();
        MemberId = context.GetCurrentMemberId();
    }
}
