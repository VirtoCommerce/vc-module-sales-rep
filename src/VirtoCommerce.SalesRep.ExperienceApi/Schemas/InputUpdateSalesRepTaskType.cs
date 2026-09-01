using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputUpdateSalesRepTaskType : ExtendableInputObjectGraphType<UpdateSalesRepTaskCommand>
{
    public InputUpdateSalesRepTaskType()
    {
        Name = "InputUpdateSalesRepTask";

        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Id))
            .Description("Id of the task to change. Must be a task the caller owns.");
        Field<NonNullGraphType<StringGraphType>>(nameof(UpdateSalesRepTaskCommand.Name))
            .Description("Task title.");
        Field<StringGraphType>(nameof(UpdateSalesRepTaskCommand.Description))
            .Description("Free-text notes.");
        Field<StringGraphType>(nameof(UpdateSalesRepTaskCommand.Type))
            .Description("One of the values configured in the TaskManagement.TaskTypes settings dictionary.");
        Field<StringGraphType>(nameof(UpdateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Defaults to Normal.");
        Field<NonNullGraphType<DateTimeGraphType>>(nameof(UpdateSalesRepTaskCommand.DueDate))
            .Description("When the task is due.");
    }
}
