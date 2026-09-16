using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class DeleteSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<DeleteSalesRepTaskCommand, bool>
{
    public DeleteSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService)
        : base(organizationAccessService, taskService)
    {
    }

    public virtual async Task<bool> Handle(DeleteSalesRepTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await GetOwnedTaskAsync(request.UserId, request.MemberId, request.Id);

        await RequireTaskService().DeleteAsync([task.Id]);

        return true;
    }
}
