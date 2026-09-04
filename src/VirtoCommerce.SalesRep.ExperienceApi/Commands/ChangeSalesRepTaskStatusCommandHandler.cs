using System.Threading;
using System.Threading.Tasks;
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

        // Not FinishAsync: it publishes WorkTaskCanceledEvent even when completing, and cannot reopen.
        task.Completed = request.Completed;
        task.IsActive = !request.Completed;

        await RequireTaskService().SaveChangesAsync([task]);

        return SalesRepTask.FromWorkTask(task);
    }
}
