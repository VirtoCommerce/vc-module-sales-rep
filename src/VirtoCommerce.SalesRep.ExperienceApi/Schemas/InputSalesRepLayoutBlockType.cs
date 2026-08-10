using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputSalesRepLayoutBlockType : ExtendableInputObjectGraphType<LayoutBlock>
{
    public InputSalesRepLayoutBlockType()
    {
        Name = "InputSalesRepLayoutBlock";

        Field<NonNullGraphType<StringGraphType>>(nameof(LayoutBlock.Id)).Description("Instance id (frontend-generated, stable across saves, unique within the layout).");
        Field<NonNullGraphType<StringGraphType>>(nameof(LayoutBlock.Type)).Description("Block type discriminator (frontend-owned vocabulary).");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(LayoutBlock.Hidden)).Description("Whether the block is parked in the hidden tray.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepLayoutSettingType>>>>(nameof(LayoutBlock.Settings))
            .Description("Block-type-specific settings (send an empty list for none).");
    }
}
