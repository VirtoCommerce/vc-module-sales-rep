using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class InputSalesRepDashboardRegionType : ExtendableInputObjectGraphType<DashboardRegion>
{
    public InputSalesRepDashboardRegionType()
    {
        Name = "InputSalesRepDashboardRegion";

        Field<NonNullGraphType<StringGraphType>>(nameof(DashboardRegion.Id)).Description("Fixed region id (e.g. \"statistics\", \"mainLeft\", \"mainRight\").");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepDashboardBlockType>>>>(nameof(DashboardRegion.Blocks))
            .Description("Blocks in render order (array position is the order).");
    }
}
