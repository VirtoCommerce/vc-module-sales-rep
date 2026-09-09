using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public abstract class SalesRepTaskCommandHandlerBase : SalesRepTaskHandlerBase
{
    protected SalesRepTaskCommandHandlerBase(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService)
        : base(organizationAccessService)
    {
        TaskService = taskService;
    }

    protected IOptionalDependency<IWorkTaskService> TaskService { get; }

    protected virtual IWorkTaskService RequireTaskService()
    {
        if (!TaskService.HasValue)
        {
            throw new ExecutionError("Task management is not available.");
        }

        return TaskService.Value;
    }

    protected virtual async Task EnsureSalesRepAsync(string userId)
    {
        if (!await IsSalesRepAsync(userId))
        {
            throw AuthorizationError.Forbidden();
        }
    }

    // Ownership is the whole security boundary, so every write re-checks the STORED ResponsibleId.
    protected virtual async Task<WorkTask> GetOwnedTaskAsync(string userId, string memberId, string taskId)
    {
        await EnsureSalesRepAsync(userId);

        if (string.IsNullOrEmpty(taskId))
        {
            throw new ExecutionError("Task id is required.");
        }

        var task = await RequireTaskService().GetByIdAsync(taskId);
        var responsibleIds = await GetVisibleResponsibleIdsAsync(userId, memberId);

        // Case-insensitive, per the convention for in-memory id comparisons. The read path compares in SQL and so
        // follows the column's collation, which is provider- and deployment-dependent: case-sensitive on PostgreSQL
        // by default, case-insensitive under SQL Server's default SQL_Latin1_General_CP1_CI_AS. Where the two differ
        // a case-variant row is writable but not listable, and the forgiving side is the right one to be on: it keeps
        // the rep's own task reachable. Aligning the collations is out of scope here.
        //
        // ONE answer for "no such task" and "not yours", so a write cannot be used to probe whether an id exists -
        // the same rule salesRepTask follows by returning null for both. Both branches also do the same work, so
        // the timing does not tell them apart either.
        if (task == null || !responsibleIds.Contains(task.ResponsibleId, StringComparer.OrdinalIgnoreCase))
        {
            throw new ExecutionError("Task not found.");
        }

        return task;
    }

    // Trimmed here: storefront inputs emit raw text and nothing downstream trims. Blank collapses to null, so a
    // field cleared on update is stored exactly like one omitted on create.
    protected virtual void ApplyInput(WorkTask task, ISalesRepTaskInput input)
    {
        ValidateInput(input);

        task.Name = input.Name?.Trim();
        task.Description = input.Description?.Trim().EmptyToNull();
        task.Type = input.Type?.Trim().EmptyToNull();
        task.Priority = ParsePriority(input.Priority);
        task.DueDate = input.DueDate;
    }

    // Here rather than in each handler: both mutations enter through ApplyInput, so a third cannot forget it.
    // The storage columns are capped and nothing between this boundary and SaveChangesAsync checks them, so an
    // over-long name would surface as a DbUpdateException. The harness is SQLite, which does not enforce VARCHAR
    // length, so no component test can catch that either.
    protected virtual void ValidateInput(ISalesRepTaskInput input)
    {
        var name = input.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new ExecutionError("Task name is required.");
        }

        if (name.Length > ModuleConstants.Tasks.MaxNameLength)
        {
            throw new ExecutionError($"Task name must not exceed {ModuleConstants.Tasks.MaxNameLength} characters.");
        }

        if (input.Type?.Trim().Length > ModuleConstants.Tasks.MaxTypeLength)
        {
            throw new ExecutionError($"Task type must not exceed {ModuleConstants.Tasks.MaxTypeLength} characters.");
        }
    }

    // Strict, unlike the module's own EnumUtility.SafeParse: a typo should be an error, not a different priority.
    // IsDefined too: TryParse alone accepts any numeric string, so "999" would parse to (TaskPriority)999.
    protected static TaskPriority ParsePriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return TaskPriority.Normal;
        }

        if (!Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out var result) || !Enum.IsDefined(result))
        {
            throw new ExecutionError($"Unknown task priority '{priority}'.");
        }

        return result;
    }
}
