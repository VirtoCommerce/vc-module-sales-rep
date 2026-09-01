using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Extensions;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<CreateSalesRepTaskCommand, SalesRepTask>
{
    private readonly IMemberService _memberService;

    public CreateSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService,
        IMemberService memberService)
        : base(organizationAccessService, taskService)
    {
        _memberService = memberService;
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

    private static string RequireMemberId(string memberId)
    {
        if (string.IsNullOrEmpty(memberId))
        {
            throw new ExecutionError("The signed-in account has no contact record, so it cannot own tasks.");
        }

        return memberId;
    }

    // The REST-only TaskAuthorizationHandler that normally denormalizes these does not run on the GraphQL path.
    private async Task AssignResponsibleAsync(WorkTask task, string memberId)
    {
        task.ResponsibleId = memberId;

        var member = await _memberService.GetByIdAsync(memberId)
            ?? throw new ExecutionError("The signed-in account's contact record no longer exists.");

        task.ResponsibleName = member.Name;
        task.OrganizationId = member.GetMemberOrganizationId();
    }
}
