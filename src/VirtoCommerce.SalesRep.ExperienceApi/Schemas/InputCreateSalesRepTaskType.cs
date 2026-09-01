using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputCreateSalesRepTaskType : ExtendableInputObjectGraphType<CreateSalesRepTaskCommand>
{
    public InputCreateSalesRepTaskType()
    {
        Name = "InputCreateSalesRepTask";

        Field<NonNullGraphType<StringGraphType>>(nameof(CreateSalesRepTaskCommand.Name))
            .Description("Task title.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Description))
            .Description("Free-text notes.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Type))
            .Description("One of the values configured in the TaskManagement.TaskTypes settings dictionary.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Defaults to Normal.");
        Field<NonNullGraphType<DateTimeGraphType>>(nameof(CreateSalesRepTaskCommand.DueDate))
            .Description("When the task is due.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.StoreId))
            .Description("Optional store to scope the task to.");
    }
}
