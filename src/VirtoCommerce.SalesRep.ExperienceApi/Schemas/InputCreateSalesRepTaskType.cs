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
            .Description("Task title (required, max 256 chars).");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Description))
            .Description("Free-text notes.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Type))
            .Description("Free text, max 128 chars, typically one of the values salesRepTaskTypes offers - not enforced.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Defaults to Normal.");
        Field<NonNullGraphType<DateTimeGraphType>>(nameof(CreateSalesRepTaskCommand.DueDate))
            .Description("When the task is due.");
    }
}
