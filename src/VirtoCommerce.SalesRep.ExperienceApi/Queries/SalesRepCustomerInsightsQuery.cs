using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerInsightsQuery : Query<SalesRepCustomerInsightsContext>
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public string CultureName { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(OrganizationId), "Organization (customer) id whose tracked activity to read.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store whose analytics configuration and events to read (defaults to all stores).");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Optional date range for the analytics figures (omit for all dates).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the product slug resolution (\"en-US\").");
    }

    public override void Map(IResolveFieldContext context)
    {
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Period = context.GetArgument<SalesRepStatisticsPeriodInput>(nameof(Period));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        UserId = context.GetCurrentUserId();
    }
}
