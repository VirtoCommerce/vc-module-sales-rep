using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.TaskManagement.Core.Models;
using VirtoCommerce.TaskManagement.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTasksQueryHandler : SalesRepTaskQueryHandlerBase, IQueryHandler<SalesRepTasksQuery, SalesRepTaskSearchResult>
{
    private readonly ISalesRepTaskFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepTaskSortRuleResolver _sortRuleResolver;

    public SalesRepTasksQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService,
        ISalesRepTaskFilterRuleResolver filterRuleResolver,
        ISalesRepTaskSortRuleResolver sortRuleResolver)
        : base(organizationAccessService, taskSearchService)
    {
        _filterRuleResolver = filterRuleResolver;
        _sortRuleResolver = sortRuleResolver;
    }

    public virtual async Task<SalesRepTaskSearchResult> Handle(SalesRepTasksQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepTaskSearchResult>.TryCreateInstance();

        var responsibleIds = await GetVisibleResponsibleIdsAsync(request.UserId, request.MemberId);
        if (responsibleIds.Count == 0 || !await CanReadAsync(request.UserId))
        {
            return result;
        }

        var criteria = BuildSearchCriteria(request, responsibleIds);

        criteria = await _sortRuleResolver.ApplySortAsync(request.StoreId, request.Sort, criteria);

        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(
            request.StoreId, request.Filter, criteria, ResolveDayStart(request.Today));
        if (filteredCriteria == null)
        {
            return result;
        }

        var searchResult = await TaskSearchService.Value.SearchAsync(filteredCriteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results
            .Select(SalesRepTask.FromWorkTask)
            .ToList();

        return result;
    }

    protected virtual WorkTaskSearchCriteria BuildSearchCriteria(SalesRepTasksQuery request, IList<string> responsibleIds)
    {
        var criteria = request.GetSearchCriteria<WorkTaskSearchCriteria>();
        criteria.ResponsibleIds = responsibleIds;
        criteria.StoreIds = string.IsNullOrEmpty(request.StoreId) ? null : [request.StoreId];
        criteria.StartDueDate = request.Period?.From;
        criteria.EndDueDate = request.Period?.To;

        // Default: attachments are not exposed, and the repository would otherwise load them for every row.
        criteria.ResponseGroup = WorkTaskResponseGroup.Default.ToString();

        return criteria;
    }
}
