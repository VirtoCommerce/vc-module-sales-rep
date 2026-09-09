using System;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepTaskQueryHandlerBase : SalesRepTaskHandlerBase
{
    protected SalesRepTaskQueryHandlerBase(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService)
        : base(organizationAccessService)
    {
        TaskSearchService = taskSearchService;
    }

    protected IOptionalDependency<IWorkTaskSearchService> TaskSearchService { get; }

    // Optional dependency: with the module absent every read is simply empty.
    protected virtual async Task<bool> CanReadAsync(string userId)
    {
        return TaskSearchService.HasValue && await IsSalesRepAsync(userId);
    }

    protected static DateTime ResolveDayStart(DateTime? today)
    {
        return today ?? DateTime.UtcNow.Date;
    }
}
