using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepLayoutSettingType : ExtendableGraphType<LayoutSetting>
{
    public SalesRepLayoutSettingType()
    {
        Name = "SalesRepLayoutSetting";

        Field(x => x.Key, nullable: false).Description("Setting key (block-type-specific, frontend-owned vocabulary).");
        Field<AnyValueGraphType>(nameof(LayoutSetting.Value)).Description("Scalar setting value (string, number, boolean).");
    }
}
