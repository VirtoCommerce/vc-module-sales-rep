using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class InputSalesRepDashboardBlockType : ExtendableInputObjectGraphType<DashboardBlock>
{
    public InputSalesRepDashboardBlockType()
    {
        Name = "InputSalesRepDashboardBlock";

        Field<NonNullGraphType<StringGraphType>>(nameof(DashboardBlock.Id)).Description("Instance id (frontend-generated, stable across saves, unique within the layout).");
        Field<NonNullGraphType<StringGraphType>>(nameof(DashboardBlock.Type)).Description("Block type discriminator (frontend-owned vocabulary).");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(DashboardBlock.Hidden)).Description("Whether the block is parked in the hidden tray.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepDashboardSettingType>>>>(nameof(DashboardBlock.Settings))
            .Description("Block-type-specific settings (send an empty list for none).");
    }
}
