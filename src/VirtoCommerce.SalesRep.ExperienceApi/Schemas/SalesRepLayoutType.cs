using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepLayoutType : ExtendableGraphType<Layout>
{
    public SalesRepLayoutType()
    {
        Name = "SalesRepLayout";

        Field(x => x.SchemaVersion, nullable: false).Description("Document schema version, for frontend migration of older saved layouts.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepLayoutRegionType>>>>(nameof(Layout.Regions))
            .Description("Top-level fixed regions.");
        Field(x => x.ModifiedDate, nullable: true).Description("When the layout was last saved (UTC).");
    }
}
