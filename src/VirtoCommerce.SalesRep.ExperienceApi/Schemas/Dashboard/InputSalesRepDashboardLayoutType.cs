using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas.Dashboard;

public class InputSalesRepDashboardLayoutType : ExtendableInputObjectGraphType<SaveDashboardLayoutCommand>
{
    public InputSalesRepDashboardLayoutType()
    {
        Name = "InputSalesRepDashboardLayout";

        Field<NonNullGraphType<StringGraphType>>(nameof(SaveDashboardLayoutCommand.Scope))
            .Description("Layout surface identifier (e.g. \"dashboard\", \"customerProfile\").");
        Field<StringGraphType>(nameof(SaveDashboardLayoutCommand.StoreId))
            .Description("Optional store to scope the layout to.");
        Field<NonNullGraphType<IntGraphType>>(nameof(SaveDashboardLayoutCommand.SchemaVersion))
            .Description("Document schema version.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepDashboardRegionType>>>>(nameof(SaveDashboardLayoutCommand.Regions))
            .Description("Top-level fixed regions with their blocks.");
    }
}
