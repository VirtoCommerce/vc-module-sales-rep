using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<CreateSalesRepTaskCommand, SalesRepTask>
{
    public CreateSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService,
        IMemberService memberService)
        : base(organizationAccessService, taskService, memberService)
    {
    }

    public virtual async Task<SalesRepTask> Handle(CreateSalesRepTaskCommand request, CancellationToken cancellationToken)
    {
        await EnsureSalesRepAsync(request.UserId);

        var memberId = RequireMemberId(request.MemberId);

        var task = AbstractTypeFactory<WorkTask>.TryCreateInstance();
        ApplyInput(task, request);
        task.IsActive = true;
        task.StoreId = request.StoreId;
        await AssignResponsibleAsync(task, memberId);

        await RequireTaskService().SaveChangesAsync([task]);

        return SalesRepTask.FromWorkTask(task);
    }
}
