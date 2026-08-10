using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputSalesRepLayoutType : ExtendableInputObjectGraphType<SaveLayoutCommand>
{
    public InputSalesRepLayoutType()
    {
        Name = "InputSalesRepLayout";

        Field<NonNullGraphType<StringGraphType>>(nameof(SaveLayoutCommand.Scope))
            .Description("Layout surface identifier (e.g. \"dashboard\", \"customerProfile\").");
        Field<StringGraphType>(nameof(SaveLayoutCommand.StoreId))
            .Description("Optional store to scope the layout to.");
        Field<NonNullGraphType<IntGraphType>>(nameof(SaveLayoutCommand.SchemaVersion))
            .Description("Document schema version.");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<InputSalesRepLayoutRegionType>>>>(nameof(SaveLayoutCommand.Regions))
            .Description("Top-level fixed regions with their blocks.");
    }
}
