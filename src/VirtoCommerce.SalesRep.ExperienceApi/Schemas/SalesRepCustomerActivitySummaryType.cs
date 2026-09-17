using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerActivitySummaryType : ExtendableGraphType<SalesRepCustomerActivitySummary>
{
    public SalesRepCustomerActivitySummaryType()
    {
        Name = "SalesRepCustomerActivitySummary";

        Field(x => x.CreatedOn, nullable: true).Description("When the organization was created (from the database, not analytics).");
        Field(x => x.LastWebLogin, nullable: true).Description("Last tracked storefront login, as the UTC start of an analytics hour bucket; null when analytics is not configured or has no data.");
        Field(x => x.VisitsCount, nullable: false).Description("Number of tracked storefront logins in the period (0 when analytics is not configured).");
        Field(x => x.LastSearchTerm, nullable: true).Description("Most recently searched phrase (null when analytics is not configured or has no data).");
        Field<SalesRepActivityProductType>("lastViewedProduct")
            .Description("Most recently viewed product (null when analytics is not configured or has no data).")
            .Resolve(context => context.Source.LastViewedProduct);
        Field(x => x.IsAnalyticsAvailable, nullable: false).Description("Whether the analytics figures beside this are measurements. False when analytics is absent, unconfigured, or could not be read — the figures are then zero/null for want of a source, not for want of activity. Non-analytics fields such as createdOn are unaffected.");
    }
}
