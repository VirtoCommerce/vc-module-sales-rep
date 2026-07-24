using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class SalesRepDashboardBlockType : ExtendableGraphType<DashboardBlock>
{
    public SalesRepDashboardBlockType()
    {
        Name = "SalesRepDashboardBlock";

        Field(x => x.Id, nullable: false).Description("Instance id (frontend-generated, stable across saves, unique within the layout).");
        Field(x => x.Type, nullable: false).Description("Block type discriminator (frontend-owned vocabulary).");
        Field(x => x.Hidden, nullable: false).Description("Whether the block is parked in the hidden tray.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepDashboardSettingType>>>>(nameof(DashboardBlock.Settings))
            .Description("Block-type-specific settings (may be empty).");
    }
}
