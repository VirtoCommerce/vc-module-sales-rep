using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepLayoutBlockType : ExtendableGraphType<LayoutBlock>
{
    public SalesRepLayoutBlockType()
    {
        Name = "SalesRepLayoutBlock";

        Field(x => x.Id, nullable: false).Description("Instance id (frontend-generated, stable across saves, unique within the layout).");
        Field(x => x.Type, nullable: false).Description("Block type discriminator (frontend-owned vocabulary).");
        Field(x => x.Hidden, nullable: false).Description("Whether the block is parked in the hidden tray.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepLayoutSettingType>>>>(nameof(LayoutBlock.Settings))
            .Description("Block-type-specific settings (may be empty).");
    }
}
