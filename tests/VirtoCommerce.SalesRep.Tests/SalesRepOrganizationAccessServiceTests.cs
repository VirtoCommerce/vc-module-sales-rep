using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepOrganizationAccessService"/> — the single "which organizations may this
/// Sales Rep act on" choke point, shared by the wishlist sharing write gate, the customer-communication handler and
/// every sales-rep query handler. A widened result here widens all of them at once, so the negative cases (no granting
/// role, locked membership, an organization the rep does not serve) are the important ones.
/// The membership search is faked but filters like the real service does, so a criteria the service forgets to set
/// (notably <c>OnlyUnlocked</c>) shows up as leaked data rather than passing silently.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrganizationAccessServiceTests
{
    private const string RepUserId = "rep-user-1";
    private const string OtherUserId = "rep-user-2";
    private const string GrantingRoleId = "role-sales-rep";
    private const string OtherRoleId = "role-buyer";
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";
    private const string OrgLocked = "org-locked";
    private const string OrgUnserved = "org-unserved";

    [Fact]
    public async Task GetGrantingMembershipsAsync_NoGrantingRoles_ReturnsEmptyWithoutSearching()
    {
        // Fails closed: with no role granting access there is nothing to scope to, and the search must not run at all
        // (an unfiltered criteria would otherwise match every membership in the system).
        var search = new FakeMembershipSearchService(Membership(RepUserId, OrgA));
        var service = new SalesRepOrganizationAccessService(new FakeRoleResolver(), search);

        var result = await service.GetGrantingMembershipsAsync([RepUserId]);

        result.Should().BeEmpty();
        search.CapturedCriteria.Should().BeEmpty();
    }

    [Fact]
    public async Task GetGrantingMembershipsAsync_RequestsUnlockedGrantingMembershipsOnly()
    {
        var search = new FakeMembershipSearchService(Membership(RepUserId, OrgA));
        var service = CreateService(search);

        await service.GetGrantingMembershipsAsync([RepUserId], [OrgA]);

        var criteria = search.CapturedCriteria.Should().ContainSingle().Subject;
        criteria.UserIds.Should().BeEquivalentTo([RepUserId]);
        criteria.OrganizationIds.Should().BeEquivalentTo([OrgA]);
        criteria.RoleIds.Should().BeEquivalentTo([GrantingRoleId]);
        criteria.OnlyUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ServesOrganizationAsync_UnlockedGrantingMembership_ReturnsTrue()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA)));

        (await service.ServesOrganizationAsync(RepUserId, OrgA)).Should().BeTrue();
    }

    [Fact]
    public async Task ServesOrganizationAsync_OrganizationNotServed_ReturnsFalse()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA)));

        (await service.ServesOrganizationAsync(RepUserId, OrgUnserved)).Should().BeFalse();
    }

    [Fact]
    public async Task ServesOrganizationAsync_LockedMembership_ReturnsFalse()
    {
        // A locked membership must not grant anything — the only guard is criteria.OnlyUnlocked.
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgLocked, isLocked: true)));

        (await service.ServesOrganizationAsync(RepUserId, OrgLocked)).Should().BeFalse();
    }

    [Fact]
    public async Task ServesOrganizationAsync_MembershipWithoutGrantingRole_ReturnsFalse()
    {
        // Being a member of the organization is not enough; the membership must carry a Sales-Rep-granting role.
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA, roleId: OtherRoleId)));

        (await service.ServesOrganizationAsync(RepUserId, OrgA)).Should().BeFalse();
    }

    [Fact]
    public async Task ServesOrganizationAsync_AnotherUsersMembership_ReturnsFalse()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(OtherUserId, OrgA)));

        (await service.ServesOrganizationAsync(RepUserId, OrgA)).Should().BeFalse();
    }

    [Fact]
    public async Task GetServedOrganizationIdsAsync_ReturnsDistinctServedOrganizations()
    {
        var service = CreateService(new FakeMembershipSearchService(
            Membership(RepUserId, OrgA),
            Membership(RepUserId, OrgA), // duplicate membership rows must not duplicate the org
            Membership(RepUserId, OrgB),
            Membership(RepUserId, OrgLocked, isLocked: true),
            Membership(RepUserId, organizationId: null),
            Membership(OtherUserId, OrgUnserved)));

        var result = await service.GetServedOrganizationIdsAsync(RepUserId);

        result.Should().BeEquivalentTo([OrgA, OrgB]);
    }

    [Fact]
    public async Task GetVisibleOrganizationIdsAsync_ServedOrganization_ReturnsThatOrganizationOnly()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA), Membership(RepUserId, OrgB)));

        var result = await service.GetVisibleOrganizationIdsAsync(RepUserId, OrgA);

        result.Should().BeEquivalentTo([OrgA]);
    }

    [Fact]
    public async Task GetVisibleOrganizationIdsAsync_UnservedOrganization_ReturnsEmpty()
    {
        // The dangerous failure mode: an unserved organization must narrow to nothing, NOT fall back to the whole book
        // (which would turn "show me customer X" into "show me all my customers" for every query handler).
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA), Membership(RepUserId, OrgB)));

        var result = await service.GetVisibleOrganizationIdsAsync(RepUserId, OrgUnserved);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVisibleOrganizationIdsAsync_NoOrganizationRequested_ReturnsAllServed()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA), Membership(RepUserId, OrgB)));

        var result = await service.GetVisibleOrganizationIdsAsync(RepUserId, organizationId: null);

        result.Should().BeEquivalentTo([OrgA, OrgB]);
    }

    [Fact]
    public async Task GetVisibleGrantingMembershipsAsync_ScopesToRequestedOrganization()
    {
        var service = CreateService(new FakeMembershipSearchService(Membership(RepUserId, OrgA), Membership(RepUserId, OrgB)));

        var scoped = await service.GetVisibleGrantingMembershipsAsync(RepUserId, OrgA);
        var all = await service.GetVisibleGrantingMembershipsAsync(RepUserId, organizationId: null);

        scoped.Select(x => x.OrganizationId).Should().BeEquivalentTo([OrgA]);
        all.Select(x => x.OrganizationId).Should().BeEquivalentTo([OrgA, OrgB]);
    }

    private static SalesRepOrganizationAccessService CreateService(IOrganizationMembershipSearchService search) =>
        new(new FakeRoleResolver(GrantingRoleId), search);

    private static OrganizationMembership Membership(string userId, string organizationId, bool isLocked = false, string roleId = GrantingRoleId) =>
        new()
        {
            Id = $"{userId}-{organizationId}-{roleId}",
            UserId = userId,
            OrganizationId = organizationId,
            IsLocked = isLocked,
            Roles = [new OrganizationMembershipRole { RoleId = roleId }],
        };

    private sealed class FakeRoleResolver(params string[] grantingRoleIds) : ISalesRepRoleResolver
    {
        public Task<ISet<string>> GetRoleIdsGrantingAccessAsync() => Task.FromResult<ISet<string>>(grantingRoleIds.ToHashSet());

        // The service under test resolves granting roles by id only; anything else is out of contract here.
        public Task<IList<Role>> GetRolesGrantingAccessAsync() => throw new NotSupportedException();

        public Task<IList<Role>> GetSelectableRolesAsync() => throw new NotSupportedException();

        public Task<Role> EnsureSalesRepRoleAsync() => throw new NotSupportedException();
    }

    /// <summary>
    /// Applies the same filters the real search service applies, so a criteria the service under test fails to set
    /// leaks rows into the result instead of being invisible.
    /// </summary>
    private sealed class FakeMembershipSearchService(params OrganizationMembership[] memberships) : IOrganizationMembershipSearchService
    {
        public List<OrganizationMembershipSearchCriteria> CapturedCriteria { get; } = [];

        public Task<OrganizationMembershipSearchResult> SearchAsync(OrganizationMembershipSearchCriteria criteria, bool clone = true)
        {
            CapturedCriteria.Add(criteria);

            var matches = memberships.Where(x => Matches(criteria, x)).ToList();
            var page = matches.Skip(criteria.Skip).Take(criteria.Take <= 0 ? matches.Count : criteria.Take).ToList();

            return Task.FromResult(new OrganizationMembershipSearchResult { TotalCount = matches.Count, Results = page });
        }

        // Not used by the service under test: it scopes exclusively through SearchAsync + criteria.
        public Task<IDictionary<string, int>> GetCountsByUserAsync(OrganizationMembershipSearchCriteria criteria) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<OrganizationRole>> GetRolesByUserAndOrgAsync(string userId, string organizationId) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<OrganizationRole>> GetRolesByUserAndOrgAsync(string organizationId, OrganizationMembership membership) => throw new NotSupportedException();

        public Task<IDictionary<string, IReadOnlyCollection<OrganizationRole>>> GetRolesForUsersInOrgAsync(IList<string> userIds, string organizationId) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<string>> GetUserIdsByRoleInOrgAsync(string organizationId, IList<string> roleIds) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<string>> GetLockedOrganizationIdsAsync(string userId) => throw new NotSupportedException();

        private static bool Matches(OrganizationMembershipSearchCriteria criteria, OrganizationMembership membership)
        {
            if (criteria.OnlyUnlocked && membership.IsLocked)
            {
                return false;
            }

            if (criteria.UserIds?.Count > 0 && !criteria.UserIds.Contains(membership.UserId))
            {
                return false;
            }

            if (criteria.OrganizationIds?.Count > 0 && !criteria.OrganizationIds.Contains(membership.OrganizationId))
            {
                return false;
            }

            return !(criteria.RoleIds?.Count > 0)
                || membership.Roles?.Any(role => criteria.RoleIds.Contains(role.RoleId)) == true;
        }
    }
}
