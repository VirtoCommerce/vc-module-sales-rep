using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepQueryHandlerBase
{
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;

    protected SalesRepQueryHandlerBase(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
    {
        _roleResolver = roleResolver;
        _membershipSearchService = membershipSearchService;
    }

    protected async Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(
        IList<string> userIds = null,
        IList<string> organizationIds = null)
    {
        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return [];
        }

        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserIds = userIds;
        criteria.OrganizationIds = organizationIds;
        criteria.RoleIds = grantingRoleIds.ToArray();
        criteria.OnlyUnlocked = true;

        return await _membershipSearchService.SearchAllNoCloneAsync(criteria);
    }

    protected async Task<IList<string>> GetServedOrganizationIdsAsync(string userId)
    {
        var memberships = await GetGrantingMembershipsAsync(userIds: [userId]);

        return memberships
            .Select(x => x.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();
    }

    protected async Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId)
    {
        if (!string.IsNullOrEmpty(organizationId))
        {
            var memberships = await GetGrantingMembershipsAsync([userId], [organizationId]);
            return memberships.Count > 0 ? [organizationId] : [];
        }

        return await GetServedOrganizationIdsAsync(userId);
    }

    protected async Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId)
    {
        return string.IsNullOrEmpty(organizationId)
            ? await GetGrantingMembershipsAsync(userIds: [userId])
            : await GetGrantingMembershipsAsync([userId], [organizationId]);
    }
}
