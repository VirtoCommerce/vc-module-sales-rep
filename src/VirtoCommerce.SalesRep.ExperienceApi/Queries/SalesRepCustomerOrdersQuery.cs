using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XOrder.Core.Queries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrdersQuery : SearchQuery<SearchOrderResponse>
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Filter { get; set; }

    public string Facet { get; set; }

    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<StringGraphType>(nameof(OrganizationId), "Organization (customer) id whose orders to load; omit for all the customers the rep serves.");
        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope the orders to (defaults to all stores).");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for the localized statusDisplayValue field and the facet labels (\"en-US\").");
        yield return Argument<StringGraphType>(nameof(Filter), "Search phrase applied to the results — a free-text keyword and/or field terms, e.g. 'status:\"New\",\"Completed\" createddate:[2026-01-01 TO 2026-02-01]'.");
        yield return Argument<StringGraphType>(nameof(Facet), "Space-separated fields to aggregate over the same results, e.g. 'status'. Counts come back in term_facets.");
        yield return Argument<StringGraphType>(nameof(Sort), "The sort expression, e.g. 'createdDate:desc'.");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));
        Filter = context.GetArgument<string>(nameof(Filter));
        Facet = context.GetArgument<string>(nameof(Facet));
        UserId = context.GetCurrentUserId();
    }
}
