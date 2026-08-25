using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepActivityConnectionType : ExtendableGraphType<SalesRepActivitySearchResult>
{
    public SalesRepActivityConnectionType()
    {
        Name = "SalesRepActivityConnection";

        Field(x => x.TotalCount, nullable: false).Description("Total number of activity rows matching the filters (across all requested categories).");

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepActivityCategoryCountType>>>>("categoryCounts")
            .Description("Per-category totals for the applied filters (zero counts included).")
            .Resolve(context => context.Source.CategoryCounts);

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepActivityEventType>>>>("items")
            .Description("The requested activity page, newest first.")
            .Resolve(context => context.Source.Results);
    }
}
