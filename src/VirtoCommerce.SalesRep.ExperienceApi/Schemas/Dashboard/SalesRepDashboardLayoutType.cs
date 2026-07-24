using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class SalesRepDashboardLayoutType : ExtendableGraphType<DashboardLayout>
{
    public SalesRepDashboardLayoutType()
    {
        Name = "SalesRepDashboardLayout";

        Field(x => x.SchemaVersion, nullable: false).Description("Document schema version, for frontend migration of older saved layouts.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepDashboardRegionType>>>>(nameof(DashboardLayout.Regions))
            .Description("Top-level fixed regions.");
        Field(x => x.ModifiedDate, nullable: true).Description("When the layout was last saved (UTC).");
    }
}
