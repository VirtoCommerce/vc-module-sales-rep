using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskQueryHandler : SalesRepTaskQueryHandlerBase, IQueryHandler<SalesRepTaskQuery, SalesRepTask>
{
    public SalesRepTaskQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService)
        : base(organizationAccessService, taskSearchService)
    {
    }

    public virtual async Task<SalesRepTask> Handle(SalesRepTaskQuery request, CancellationToken cancellationToken)
    {
        var responsibleIds = await GetVisibleResponsibleIdsAsync(request.UserId, request.MemberId);
        if (string.IsNullOrEmpty(request.Id) || responsibleIds.Count == 0 || !await CanReadAsync(request.UserId))
        {
            return null;
        }

        // Read through the search service with the ownership filter applied, rather than loading by id and comparing
        // afterwards: someone else's task then returns null exactly like a missing one, leaking no existence.
        var criteria = AbstractTypeFactory<WorkTaskSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = [request.Id];
        criteria.ResponsibleIds = responsibleIds;
        criteria.Take = 1;
        criteria.ResponseGroup = WorkTaskResponseGroup.Default.ToString();

        var searchResult = await TaskSearchService.Value.SearchAsync(criteria);
        var task = searchResult.Results.FirstOrDefault();

        return task == null ? null : SalesRepTask.FromWorkTask(task);
    }
}
