using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class SalesRepDashboardRegionType : ExtendableGraphType<DashboardRegion>
{
    public SalesRepDashboardRegionType()
    {
        Name = "SalesRepDashboardRegion";

        Field(x => x.Id, nullable: false).Description("Fixed region id (e.g. \"statistics\", \"mainLeft\", \"mainRight\").");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepDashboardBlockType>>>>(nameof(DashboardRegion.Blocks))
            .Description("Blocks in render order (array position is the order).");
    }
}
