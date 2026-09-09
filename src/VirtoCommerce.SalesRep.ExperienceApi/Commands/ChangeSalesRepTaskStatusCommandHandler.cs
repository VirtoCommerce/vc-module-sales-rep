using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class ChangeSalesRepTaskStatusCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<ChangeSalesRepTaskStatusCommand, SalesRepTask>
{
    public ChangeSalesRepTaskStatusCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService)
        : base(organizationAccessService, taskService)
    {
    }

    public virtual async Task<SalesRepTask> Handle(ChangeSalesRepTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await GetOwnedTaskAsync(request.UserId, request.MemberId, request.Id);

        // Closed without completing means cancelled (FinishAsync(completed: false)) or timed out (TimeoutAsync,
        // which leaves Completed null). Neither is ours to reopen or to promote to done - the assignments below
        // would turn a cancellation into an ordinary open task, one-way, because nothing here can cancel it again.
        if (!task.IsActive && task.Completed != true)
        {
            throw new ExecutionError("This task was closed and can no longer be reopened or completed.");
        }

        // Not FinishAsync: it publishes WorkTaskCanceledEvent even when completing, and cannot reopen.
        task.Completed = request.Completed;
        task.IsActive = !request.Completed;

        await RequireTaskService().SaveChangesAsync([task]);

        return SalesRepTask.FromWorkTask(task);
    }
}
