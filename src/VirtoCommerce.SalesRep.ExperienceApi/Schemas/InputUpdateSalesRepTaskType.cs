using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputUpdateSalesRepTaskType : ExtendableInputObjectGraphType<UpdateSalesRepTaskCommand>
{
    // IMPORTANT (keep): every editable field is non-null. The update REPLACES the task, so an optional field
    // omitted by the client would be indistinguishable from one cleared on purpose, and a rename would silently
    // drop the description, the type and the priority.
    public InputUpdateSalesRepTaskType()
    {
        Name = "InputUpdateSalesRepTask";

        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Id))
            .Description("Id of the task to change. Must be a task the caller owns.");
        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Name))
            .Description("Task title.");
        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Description))
            .Description("Free-text notes. Send the stored value back unchanged to keep it; empty string clears it.");
        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Type))
            .Description("One of the values configured in the TaskManagement.TaskTypes settings dictionary. Empty string clears it.");
        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Empty string means Normal.");
        Field<NonNullGraphType<DateTimeGraphType>>(nameof(UpdateSalesRepTaskCommand.DueDate))
            .Description("When the task is due.");
    }
}
