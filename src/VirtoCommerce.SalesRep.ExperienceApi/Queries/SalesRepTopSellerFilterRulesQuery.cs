using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTopSellerFilterRulesQuery : Query<IList<SalesRepTopSellerFilterRule>>, ISalesRepRulesQuery, ISalesRepScopedRulesQuery
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string OrganizationId { get; set; }

    public SalesRepStatisticsPeriodInput Period { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(StoreId), "Store whose catalog the sold-into top-level categories are resolved against.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for localized category labels (\"en-US\").");
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Scope the offered categories to one customer's sales — pass the same organizationId the Top Sellers list uses. Omit for all the rep's customers.");
        yield return Argument<SalesRepStatisticsPeriodInputType>(nameof(Period), "Scope the offered categories to the sales in this created-date range — pass the same period the Top Sellers list uses. Omit for lifetime sales.");
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
