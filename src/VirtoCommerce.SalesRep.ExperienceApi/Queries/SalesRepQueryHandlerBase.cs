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
            return Array.Empty<OrganizationMembership>();
        }

        return await _membershipSearchService.SearchAllNoCloneAsync(new OrganizationMembershipSearchCriteria
        {
            UserIds = userIds,
            OrganizationIds = organizationIds,
            RoleIds = grantingRoleIds.ToArray(),
            OnlyUnlocked = true,
        });
    }
}
