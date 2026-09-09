using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskSortRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepTaskSortRulesQuery, SalesRepTaskSortRule>
{
    private readonly IOptionalDependency<IWorkTaskSearchService> _taskSearchService;
    private readonly ISalesRepTaskSortRuleResolver _sortRuleResolver;

    public SalesRepTaskSortRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService,
        ISalesRepTaskSortRuleResolver sortRuleResolver)
        : base(organizationAccessService)
    {
        _taskSearchService = taskSearchService;
        _sortRuleResolver = sortRuleResolver;
    }

    // Gated on the storage module like every other task read.
    protected override async Task<IList<string>> ResolveScopeAsync(SalesRepTaskSortRulesQuery request)
        => _taskSearchService.HasValue ? await base.ResolveScopeAsync(request) : null;

    protected override Task<IList<SalesRepTaskSortRule>> GetRulesAsync(SalesRepTaskSortRulesQuery request, IList<string> organizationIds)
        => _sortRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
