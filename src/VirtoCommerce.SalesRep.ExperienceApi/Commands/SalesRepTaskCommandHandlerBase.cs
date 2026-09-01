using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
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

    /// <summary>
    /// Loads a task the caller is allowed to change, or throws. Ownership is the whole security boundary here, so it is
    /// re-checked against the stored ResponsibleId on every write - a client-supplied id proves nothing.
    /// </summary>
    protected virtual async Task<WorkTask> GetOwnedTaskAsync(string userId, string memberId, string taskId)
    {
        await EnsureSalesRepAsync(userId);

        if (string.IsNullOrEmpty(taskId))
        {
            throw new ExecutionError("Task id is required.");
        }

        var task = await RequireTaskService().GetByIdAsync(taskId);
        if (task == null)
        {
            throw new ExecutionError("Task not found.");
        }

        var responsibleIds = await GetVisibleResponsibleIdsAsync(userId, memberId);
        if (!responsibleIds.Contains(task.ResponsibleId, StringComparer.OrdinalIgnoreCase))
        {
            throw AuthorizationError.Forbidden();
        }

        return task;
    }

    /// <summary>
    /// Copies the editable fields onto a task. Trims here because storefront inputs emit raw text and nothing
    /// downstream trims. Due date is enforced by the schema - the input types declare it non-null.
    /// </summary>
    protected virtual void ApplyInput(WorkTask task, ISalesRepTaskInput input)
    {
        task.Name = input.Name?.Trim();
        task.Description = input.Description?.Trim();
        task.Type = input.Type?.Trim();
        task.Priority = ParsePriority(input.Priority);
        task.DueDate = input.DueDate;
    }

    /// <summary>
    /// Parsing, not validation: the model holds a TaskPriority, so an unknown name cannot be carried any further and
    /// has to be rejected at the boundary. Strict, unlike the module's own EnumUtility.SafeParse fallback - a typo
    /// from a client should be an error, not a silently different priority.
    /// </summary>
    protected static TaskPriority ParsePriority(string priority)
    {
        if (string.IsNullOrEmpty(priority))
        {
            return TaskPriority.Normal;
        }

        if (!Enum.TryParse<TaskPriority>(priority, ignoreCase: true, out var result))
        {
            throw new ExecutionError($"Unknown task priority '{priority}'.");
        }

        return result;
    }
}
