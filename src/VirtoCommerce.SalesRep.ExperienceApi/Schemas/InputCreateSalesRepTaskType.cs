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
            .Description("Free text. Typically one of the values salesRepTaskTypes offers, but that is a convention the server does not enforce - the dictionary is editable at runtime.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Defaults to Normal.");
        Field<NonNullGraphType<DateTimeGraphType>>(nameof(CreateSalesRepTaskCommand.DueDate))
            .Description("When the task is due.");
    }
}
