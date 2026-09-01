using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class DeleteSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<DeleteSalesRepTaskCommand, bool>
{
    public DeleteSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService,
        IMemberService memberService)
        : base(organizationAccessService, taskService, memberService)
    {
    }

    public virtual async Task<bool> Handle(DeleteSalesRepTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await GetOwnedTaskAsync(request.UserId, request.MemberId, request.Id);

        await RequireTaskService().DeleteAsync([task.Id]);

        return true;
    }
}
