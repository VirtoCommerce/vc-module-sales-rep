using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class SalesRepDashboardSettingType : ExtendableGraphType<DashboardSetting>
{
    public SalesRepDashboardSettingType()
    {
        Name = "SalesRepDashboardSetting";

        Field(x => x.Key, nullable: false).Description("Setting key (block-type-specific, frontend-owned vocabulary).");
        Field<AnyValueGraphType>(nameof(DashboardSetting.Value)).Description("Scalar setting value (string, number, boolean).");
    }
}
