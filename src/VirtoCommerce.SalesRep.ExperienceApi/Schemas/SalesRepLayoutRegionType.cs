using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepLayoutRegionType : ExtendableGraphType<LayoutRegion>
{
    public SalesRepLayoutRegionType()
    {
        Name = "SalesRepLayoutRegion";

        Field(x => x.Id, nullable: false).Description("Fixed region id (e.g. \"statistics\", \"mainLeft\", \"mainRight\").");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepLayoutBlockType>>>>(nameof(LayoutRegion.Blocks))
            .Description("Blocks in render order (array position is the order).");
    }
}
