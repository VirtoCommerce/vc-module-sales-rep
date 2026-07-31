using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQuery : Query<IList<SalesRepOrderFilterRule>>, ISalesRepRulesQuery, ISalesRepScopedRulesQuery
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string OrganizationId { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store whose orders the offered statuses are read from.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized status labels (\"en-US\").");
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Scope the offered statuses to one customer's orders — pass the same organizationId the orders list uses. Omit for all the rep's customers.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Scope the offered statuses to orders created in this range — pass the same period the orders list uses. Omit for all dates.");
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        UserId = context.GetCurrentUserId();
    }
}
