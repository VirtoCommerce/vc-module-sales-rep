using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQuery : Query<IList<SalesRepOrderFilterRule>>, ISalesRepRulesQuery
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string OrganizationId { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store whose orders the offered statuses are read from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized status labels (\"en-US\").");
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Scope the offered statuses to one customer's orders — pass the same organizationId the orders list uses. Omit for all the rep's customers.");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        UserId = context.GetCurrentUserId();
    }
}
