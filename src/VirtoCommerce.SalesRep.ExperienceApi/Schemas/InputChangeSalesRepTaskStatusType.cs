using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputChangeSalesRepTaskStatusType : ExtendableInputObjectGraphType<ChangeSalesRepTaskStatusCommand>
{
    public InputChangeSalesRepTaskStatusType()
    {
        Name = "InputChangeSalesRepTaskStatus";

        Field<NonNullGraphType<StringGraphType>>(nameof(ChangeSalesRepTaskStatusCommand.Id))
            .Description("Id of the task to change. Must be a task the caller owns.");
        Field<NonNullGraphType<BooleanGraphType>>(nameof(ChangeSalesRepTaskStatusCommand.Completed))
            .Description("True marks the task done; false reopens it.");
    }
}
