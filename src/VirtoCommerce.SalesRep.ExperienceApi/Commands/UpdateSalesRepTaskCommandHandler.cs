using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class UpdateSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<UpdateSalesRepTaskCommand, SalesRepTask>
{
    public UpdateSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService,
        IMemberService memberService)
        : base(organizationAccessService, taskService, memberService)
    {
    }

    public virtual async Task<SalesRepTask> Handle(UpdateSalesRepTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await GetOwnedTaskAsync(request.UserId, request.MemberId, request.Id);

        ApplyInput(task, request);

        await RequireTaskService().SaveChangesAsync([task]);

        return SalesRepTask.FromWorkTask(task);
    }
}
