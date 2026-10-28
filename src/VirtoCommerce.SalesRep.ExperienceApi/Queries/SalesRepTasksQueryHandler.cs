using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core;
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

        var filteredCriteria = await ApplyFiltersAsync(request, criteria);
        if (filteredCriteria == null)
        {
            return result;
        }

        // Ownership is this handler's to enforce, not the resolvers'. Both are replaceable extension points, and one
        // that builds a fresh criteria instead of narrowing the one it was handed would otherwise search every task.
        filteredCriteria.ResponsibleIds = responsibleIds;

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

    // Each filter narrows the criteria, or finds that nothing can match and returns null: an unrecognized rule or
    // priority, or only customers the caller does not serve. None of them may fall through to the unfiltered list.
    protected virtual async Task<WorkTaskSearchCriteria> ApplyFiltersAsync(SalesRepTasksQuery request, WorkTaskSearchCriteria criteria)
    {
        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(
            request.StoreId, request.Filter, criteria, ResolveDayStart(request.Today));
        filteredCriteria = filteredCriteria == null ? null : ApplyPriorities(request, filteredCriteria);

        return filteredCriteria == null ? null : await ApplyCustomersAsync(request, filteredCriteria);
    }

    // Names in any case, as the writes take them. Any of the priorities matches, among those a filter rule allows.
    protected virtual WorkTaskSearchCriteria ApplyPriorities(SalesRepTasksQuery request, WorkTaskSearchCriteria criteria)
    {
        if (request.Priorities.IsNullOrEmpty())
        {
            return criteria;
        }

        var priorities = new List<string>();
        foreach (var name in request.Priorities)
        {
            if (!TryParsePriority(name, out var priority))
            {
                return null;
            }

            priorities.Add(priority.ToString());
        }

        // Narrowed, not assigned: a filter rule may have restricted the priorities already.
        var allowed = criteria.Priorities;
        var matching = priorities
            .Distinct()
            .Where(x => allowed.IsNullOrEmpty() || allowed.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (matching.Count == 0)
        {
            return null;
        }

        criteria.Priorities = matching;

        return criteria;
    }

    // Only customers the caller serves: the rule that decides whether a task names its customer at all, so a task whose
    // customer is hidden from the rep cannot be found by that customer either. Any of the customers matches.
    protected virtual async Task<WorkTaskSearchCriteria> ApplyCustomersAsync(SalesRepTasksQuery request, WorkTaskSearchCriteria criteria)
    {
        if (request.CustomerIds.IsNullOrEmpty())
        {
            return criteria;
        }

        // Narrowed, not assigned: a filter rule may have restricted the linked records already.
        var linkedType = criteria.LinkedObjectType;
        if (!linkedType.IsNullOrEmpty() && !linkedType.EqualsIgnoreCase(ModuleConstants.Tasks.CustomerObjectType))
        {
            return null;
        }

        var servedIds = await OrganizationAccessService.GetServedOrganizationIdsAsync(request.UserId);
        var allowedIds = criteria.LinkedObjectIds;
        var customerIds = request.CustomerIds
            .Intersect(servedIds, StringComparer.OrdinalIgnoreCase)
            .Where(x => allowedIds.IsNullOrEmpty() || allowedIds.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (customerIds.Count == 0)
        {
            return null;
        }

        criteria.LinkedObjectType = ModuleConstants.Tasks.CustomerObjectType;
        criteria.LinkedObjectIds = customerIds;

        return criteria;
    }
}
