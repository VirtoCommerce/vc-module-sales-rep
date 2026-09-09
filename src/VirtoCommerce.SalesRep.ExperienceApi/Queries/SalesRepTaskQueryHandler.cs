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

        // Filtered read: ResponsibleId is compared in SQL, so this half follows the column's collation, while the
        // write path loads by id and compares with OrdinalIgnoreCase (see GetOwnedTaskAsync). That asymmetry is what
        // makes a case-variant row writable but not listable. Both shapes hide another rep's task equally well - the
        // choice here is only about where the comparison happens.
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
