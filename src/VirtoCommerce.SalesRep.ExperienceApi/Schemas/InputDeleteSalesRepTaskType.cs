using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Commands;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class InputDeleteSalesRepTaskType : ExtendableInputObjectGraphType<DeleteSalesRepTaskCommand>
{
    public InputDeleteSalesRepTaskType()
    {
        Name = "InputDeleteSalesRepTask";

        Field<NonNullGraphType<StringGraphType>>(nameof(DeleteSalesRepTaskCommand.Id))
            .Description("Id of the task to delete. Must be a task the caller owns.");
    }
}
