using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class CustomersSalesRepActivitySource : ISalesRepActivitySource
{
    private readonly ISalesRepOrganizationAccessService _organizationAccessService;

    public CustomersSalesRepActivitySource(ISalesRepOrganizationAccessService organizationAccessService)
    {
        _organizationAccessService = organizationAccessService;
    }

    public IList<string> Categories { get; } = [ModuleConstants.Activities.Categories.Customers];

    public virtual async Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        if (criteria.GetEffectiveCategories(Categories).Count == 0 || criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        var memberships = await _organizationAccessService.GetGrantingMembershipsAsync([criteria.SalesRepUserId], criteria.OrganizationIds);

        var assignments = memberships
            .Where(x => !string.IsNullOrEmpty(x.OrganizationId))
            .GroupBy(x => x.OrganizationId)
            .Select(g => (OrganizationId: g.Key, AssignedDate: g.Min(x => x.CreatedDate)))
            .Where(x => (criteria.From == null || x.AssignedDate >= criteria.From) &&
                        (criteria.To == null || x.AssignedDate <= criteria.To))
            .OrderByDescending(x => x.AssignedDate)
            .ThenBy(x => x.OrganizationId, StringComparer.Ordinal)
            .ToList();

        result.TotalCount = assignments.Count;
        result.Results = assignments
            .Skip(criteria.Skip)
            .Take(Math.Max(criteria.Take, 0))
            .Select(x => ToEvent(x.OrganizationId, x.AssignedDate))
            .ToList();

        return result;
    }

    protected virtual SalesRepActivityEvent ToEvent(string organizationId, DateTime assignedDate)
    {
        var result = AbstractTypeFactory<SalesRepActivityEvent>.TryCreateInstance();

        result.Category = ModuleConstants.Activities.Categories.Customers;
        result.Type = ModuleConstants.Activities.Types.CustomerAssigned;
        result.OccurredAt = assignedDate;
        result.Precision = ModuleConstants.Activities.Precision.Exact;
        result.OrganizationId = organizationId;

        return result;
    }
}
