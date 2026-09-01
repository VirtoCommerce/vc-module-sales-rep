using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Extensions;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class CreateSalesRepTaskCommandHandler : SalesRepTaskCommandHandlerBase, IRequestHandler<CreateSalesRepTaskCommand, SalesRepTask>
{
    private readonly IMemberService _memberService;
    private readonly IUserSearchService _userSearchService;

    public CreateSalesRepTaskCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskService> taskService,
        IMemberService memberService,
        IUserSearchService userSearchService)
        : base(organizationAccessService, taskService)
    {
        _memberService = memberService;
        _userSearchService = userSearchService;
    }

    public virtual async Task<SalesRepTask> Handle(CreateSalesRepTaskCommand request, CancellationToken cancellationToken)
    {
        await EnsureSalesRepAsync(request.UserId);

        var memberId = RequireMemberId(request.MemberId);

        var task = AbstractTypeFactory<WorkTask>.TryCreateInstance();
        ApplyInput(task, request);
        task.IsActive = true;
        task.StoreId = await ResolveStoreIdAsync(request.UserId);
        await AssignResponsibleAsync(task, memberId);

        await RequireTaskService().SaveChangesAsync([task]);

        return SalesRepTask.FromWorkTask(task);
    }

    // The rep's own account store, never client input: the store a task belongs to is part of who owns it, and the
    // same value the customer and rep lists scope on.
    protected virtual async Task<string> ResolveStoreIdAsync(string userId)
    {
        var criteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = [userId];
        criteria.Take = 1;

        var user = (await _userSearchService.SearchUsersAsync(criteria)).Results.FirstOrDefault();

        return user?.StoreId;
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
