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

/// <summary>
/// Shared base for the Sales Rep queries. Single-sources the security scoping used by all of them: the set of
/// organization memberships whose role grants sales-rep access and that are not locked
/// (<see cref="OrganizationMembershipSearchCriteria.OnlyUnlocked"/>). Keeping this definition in one place
/// prevents the query handlers from drifting on what "grants a rep access to an organization" means.
/// </summary>
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

    /// <summary>
    /// Active (unlocked) memberships whose role grants sales-rep access, optionally scoped to the given users
    /// and/or organizations. Returns an empty list when no role grants access.
    /// </summary>
    protected async Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(
        string[] userIds = null,
        string[] organizationIds = null)
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

    /// <summary>
    /// The distinct organizations the given rep is assigned to serve — the organizations of their active,
    /// unlocked sales-rep-granting memberships. Empty when the rep serves none. This is the "customers this rep
    /// serves" set; keeping it here (not re-derived per handler) is what stops the handlers from drifting on it.
    /// </summary>
    protected async Task<string[]> GetServedOrganizationIdsAsync(string userId)
    {
        var memberships = await GetGrantingMembershipsAsync(userIds: [userId]);

        return memberships
            .Select(x => x.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// The organizations a query may read for an optional single-organization filter: when
    /// <paramref name="organizationId"/> is given, just that organization if the rep actively serves it (else none —
    /// so a rep can't read an organization they don't serve by guessing its id); when omitted, every organization
    /// the rep is assigned to (the cross-customer view). Shared by the orders and statistics queries so they scope
    /// identically.
    /// </summary>
    protected async Task<string[]> GetVisibleOrganizationIdsAsync(string userId, string organizationId)
    {
        if (!string.IsNullOrEmpty(organizationId))
        {
            var memberships = await GetGrantingMembershipsAsync([userId], [organizationId]);
            return memberships.Count > 0 ? [organizationId] : [];
        }

        return await GetServedOrganizationIdsAsync(userId);
    }

    /// <summary>
    /// The rep's active granting memberships for the visible scope: the requested organization's membership(s) when
    /// <paramref name="organizationId"/> is given and the rep serves it (else none), otherwise every granting membership
    /// the rep has. Parallels <see cref="GetVisibleOrganizationIdsAsync"/> but returns the membership objects, so a
    /// caller that also needs assignment dates (e.g. the "new customers" counter) resolves them without re-querying.
    /// </summary>
    protected async Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId)
    {
        return string.IsNullOrEmpty(organizationId)
            ? await GetGrantingMembershipsAsync(userIds: [userId])
            : await GetGrantingMembershipsAsync([userId], [organizationId]);
    }
}
