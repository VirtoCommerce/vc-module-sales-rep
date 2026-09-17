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

        Field(x => x.IsAnalyticsAvailable, nullable: false)
            .Description(
                "Whether tracked storefront activity is measured for this store right now. False means the " +
                "searches, product views and sign-in counts are zero because nothing is being measured — " +
                "analytics is absent, unconfigured, or could not be read — not because the customer was " +
                "inactive. It carries no detail about which: the server log and the diagnostics endpoint do.");

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepActivityCategoryCountType>>>>("categoryCounts")
            .Description("Per-category totals for the applied filters (zero counts included).")
            .Resolve(context => context.Source.CategoryCounts);

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepActivityEventType>>>>("items")
            .Description("The requested activity page, newest first.")
            .Resolve(context => context.Source.Results);
    }
}
