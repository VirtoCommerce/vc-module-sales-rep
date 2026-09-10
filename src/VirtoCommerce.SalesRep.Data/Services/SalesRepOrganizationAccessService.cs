using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepOrganizationAccessService(
    ISalesRepRoleResolver roleResolver,
    IOrganizationMembershipSearchService membershipSearchService) : ISalesRepOrganizationAccessService
{
    public virtual async Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(
        IList<string> userIds = null,
        IList<string> organizationIds = null)
    {
        var grantingRoleIds = await roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return [];
        }

        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserIds = userIds;
        criteria.OrganizationIds = organizationIds;
        criteria.RoleIds = grantingRoleIds.ToArray();
        criteria.OnlyUnlocked = true;

        return await membershipSearchService.SearchAllNoCloneAsync(criteria);
    }

    public virtual async Task<bool> ServesOrganizationAsync(string userId, string organizationId)
    {
        var memberships = await GetGrantingMembershipsAsync([userId], [organizationId]);
        return memberships.Count > 0;
    }

    // One membership query for the whole set; nothing to check is fine, an empty id is never served.
    public virtual async Task<bool> ServesAllOrganizationsAsync(string userId, IList<string> organizationIds)
    {
        if (organizationIds.IsNullOrEmpty())
        {
            return true;
        }

        if (string.IsNullOrEmpty(userId) || organizationIds.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var memberships = await GetGrantingMembershipsAsync([userId], organizationIds);
        var servedOrganizationIds = memberships.Select(x => x.OrganizationId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return organizationIds.All(servedOrganizationIds.Contains);
    }

    public virtual Task<IList<string>> GetServedOrganizationIdsAsync(string userId)
    {
        return GetVisibleOrganizationIdsAsync(userId, organizationId: null);
    }

    public virtual async Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId)
    {
        var memberships = await GetVisibleGrantingMembershipsAsync(userId, organizationId);

        return memberships
            .Select(x => x.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public virtual async Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId)
    {
        return string.IsNullOrEmpty(organizationId)
            ? await GetGrantingMembershipsAsync(userIds: [userId])
            : await GetGrantingMembershipsAsync([userId], [organizationId]);
    }
}
