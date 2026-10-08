using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputCreateSalesRepTaskType : ExtendableInputObjectGraphType<CreateSalesRepTaskCommand>
{
    // IMPORTANT (keep): create, read and update share one field shape - whatever salesRepTask can RETURN, both inputs
    // must ACCEPT, or a client cannot write back what it just read. Every column behind these is nullable, so the read
    // is the reference and non-null here would be an invention: it is what made a task with no description, or one with
    // no due date, unwritable. Product rules that are stricter than the storage (a due date is required on CREATE) live
    // in validation, where they can say so - not in the type system, where they also break update.
    public InputCreateSalesRepTaskType()
    {
        Name = "InputCreateSalesRepTask";

        Field<NonNullGraphType<StringGraphType>>(nameof(CreateSalesRepTaskCommand.Name))
            .Description("Task title (required, max 256 chars).");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Description))
            .Description("Free-text notes. Null or empty clears it.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Type))
            .Description("Free text, max 128 chars, typically one of the values salesRepTaskTypes offers - not enforced. Null or empty clears it.");
        Field<StringGraphType>(nameof(CreateSalesRepTaskCommand.Priority))
            .Description("Lowest, Low, Normal, High or Highest. Null or empty means Normal.");
        Field<DateTimeGraphType>(nameof(CreateSalesRepTaskCommand.DueDate))
            .Description("When the task is due. Required on create; null on update clears it.");
    }
}
