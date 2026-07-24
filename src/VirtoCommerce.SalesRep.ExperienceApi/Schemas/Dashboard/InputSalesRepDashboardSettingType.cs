using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.Xapi.Core.Schemas.ScalarTypes;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class InputSalesRepDashboardSettingType : ExtendableInputObjectGraphType<DashboardSetting>
{
    public InputSalesRepDashboardSettingType()
    {
        Name = "InputSalesRepDashboardSetting";

        Field<NonNullGraphType<StringGraphType>>(nameof(DashboardSetting.Key)).Description("Setting key (block-type-specific, frontend-owned vocabulary).");
        Field<AnyValueGraphType>(nameof(DashboardSetting.Value)).Description("Scalar setting value (string, number, boolean).");
    }
}
