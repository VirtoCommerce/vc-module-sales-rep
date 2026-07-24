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

public class SalesRepOrdersQuery : SearchQuery<SalesRepOrderSearchResult>, IHasIncludeFields
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public IList<string> Statuses { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

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

        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Statuses = context.GetArgument<string[]>(nameof(Statuses));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();

        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
