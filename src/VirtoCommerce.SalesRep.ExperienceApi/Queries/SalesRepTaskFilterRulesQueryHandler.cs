using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.TaskManagement.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepTaskFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepTaskFilterRulesQuery, SalesRepTaskFilterRule>
{
    private readonly IOptionalDependency<IWorkTaskSearchService> _taskSearchService;

    public SalesRepTaskFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IOptionalDependency<IWorkTaskSearchService> taskSearchService,
        ISalesRepTaskFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _taskSearchService = taskSearchService;
        FilterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepTaskFilterRule> FilterRuleResolver { get; }

    // Gated on the storage module like every other task read. Without it these tabs would still render, badged
    // zero, over a list that can never return a row - the opposite of the neutral empty state an unconfigured
    // surface is meant to show.
    protected override async Task<IList<string>> ResolveScopeAsync(SalesRepTaskFilterRulesQuery request)
        => _taskSearchService.HasValue ? await base.ResolveScopeAsync(request) : null;
}
