using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepTaskType : ExtendableGraphType<SalesRepTask>
{
    public SalesRepTaskType()
    {
        Name = "SalesRepTask";

        Field(x => x.Id, nullable: false).Description("Task id.");
        Field(x => x.Name, nullable: false).Description("Task title.");
        Field(x => x.Description, nullable: true).Description("Free-text notes.");
        Field(x => x.Type, nullable: true).Description("Task type - one of the values configured in the TaskManagement.TaskTypes settings dictionary.");
        Field(x => x.Priority, nullable: true).Description("Priority name: Lowest, Low, Normal, High or Highest.");
        Field(x => x.DueDate, nullable: true).Description("When the task is due, as an instant.");
        Field(x => x.IsActive, nullable: false).Description("False once the task has been completed or cancelled.");
        Field(x => x.Completed, nullable: true).Description("True when finished as done; false or null on a cancelled task. Combine with isActive and dueDate to render the status: active and due before the start of the viewer's today = overdue, active otherwise = upcoming, completed = done.");
        Field(x => x.CreatedDate, nullable: false).Description("When the task was created.");
        Field(x => x.ModifiedDate, nullable: true).Description("When the task was last changed.");
    }
}
