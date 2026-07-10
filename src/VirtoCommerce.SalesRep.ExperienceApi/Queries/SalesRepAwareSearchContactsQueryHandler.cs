using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.ProfileExperienceApiModule.Data.Commands;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Overrides the ProfileExperienceApi contacts search so an organization's contact roster
/// (storefront <c>organization.contacts</c>) omits the Sales Reps serving that organization.
/// <para>
/// Extends the stock <see cref="SearchMembersQueryHandler"/> and reuses its
/// <see cref="SearchMembersQueryHandler.BuildMembersSearchCriteria"/> seam, so the base contacts search can't
/// drift; only the rep-exclusion is added.
/// </para>
/// <para>
/// Scoped to organization-scoped queries: only <c>organization.contacts</c> sets a
/// <see cref="SearchMembersQueryBase.MemberId"/>. The global <c>Query.contacts</c> search leaves it empty and
/// legitimately returns every contact (reps included), so that path is passed straight through.
/// </para>
/// <para>
/// Registered as the <see cref="SearchContactsQuery"/> handler in the DI container (see the module's
/// ExperienceApi wiring); relies on this module initializing after ProfileExperienceApiModule (manifest
/// dependency) so it wins the "last registration" over the built-in handler.
/// </para>
/// </summary>
public class SalesRepAwareSearchContactsQueryHandler : SearchMembersQueryHandler
{
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly IUserSearchService _userSearchService;

    public SalesRepAwareSearchContactsQueryHandler(
        IMemberSearchService memberSearchService,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRoleResolver roleResolver,
        IUserSearchService userSearchService)
        : base(memberSearchService, membershipSearchService)
    {
        _memberSearchService = memberSearchService;
        _membershipSearchService = membershipSearchService;
        _roleResolver = roleResolver;
        _userSearchService = userSearchService;
    }

    public override async Task<MemberSearchResult> Handle(SearchContactsQuery request, CancellationToken cancellationToken)
    {
        // Reuse the base criteria builder (protected virtual) so this stays in lock-step with the stock search.
        var criteria = BuildMembersSearchCriteria(request, nameof(Contact));

        // Org-scoped roster only. Global "all contacts" search (no MemberId) is left untouched.
        if (!string.IsNullOrEmpty(request.MemberId))
        {
            var repContactIds = await GetSalesRepContactIdsAsync(request.MemberId);
            if (repContactIds.Count > 0)
            {
                // ExcludedObjectIds is applied before pagination, so page sizes and TotalCount stay consistent.
                criteria.ExcludedObjectIds = (criteria.ExcludedObjectIds ?? [])
                    .Concat(repContactIds)
                    .Distinct()
                    .ToArray();
            }
        }

        return await _memberSearchService.SearchMembersAsync(criteria);
    }

    /// <summary>
    /// Contact (member) ids of the Sales Reps serving <paramref name="organizationId"/>: users holding a role
    /// granting sales-rep access via a membership in that organization (locked or not — a blocked rep is still a
    /// rep), mapped to their contact ids. Resolved from the same "source B" primitives as SalesRepSearchService,
    /// so "who is a rep" can't drift. Global-role-only reps aren't treated as serving a specific org, so they're
    /// not excluded here.
    /// </summary>
    protected virtual async Task<IReadOnlyCollection<string>> GetSalesRepContactIdsAsync(string organizationId)
    {
        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return [];
        }

        var membershipCriteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        membershipCriteria.RoleIds = grantingRoleIds.ToArray();
        membershipCriteria.OrganizationIds = [organizationId];

        var repUserIds = (await _membershipSearchService.GetCountsByUserAsync(membershipCriteria)).Keys.ToArray();
        if (repUserIds.Length == 0)
        {
            return [];
        }

        // Map the rep accounts to their contact (member) ids — that's what the organization's roster is keyed on.
        var userCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        userCriteria.ObjectIds = repUserIds;
        userCriteria.Take = repUserIds.Length;

        var users = (await _userSearchService.SearchUsersAsync(userCriteria)).Results;

        return users
            .Where(u => !string.IsNullOrEmpty(u.MemberId))
            .Select(u => u.MemberId)
            .Distinct()
            .ToArray();
    }
}
