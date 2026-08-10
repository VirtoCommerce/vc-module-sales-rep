using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputSalesRepLayoutRegionType : ExtendableInputObjectGraphType<LayoutRegion>
{
    public InputSalesRepLayoutRegionType()
    {
        Name = "InputSalesRepLayoutRegion";

        Field<NonNullGraphType<StringGraphType>>(nameof(LayoutRegion.Id)).Description("Fixed region id (e.g. \"statistics\", \"mainLeft\", \"mainRight\").");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepLayoutBlockType>>>>(nameof(LayoutRegion.Blocks))
            .Description("Blocks in render order (array position is the order).");
    }
}
