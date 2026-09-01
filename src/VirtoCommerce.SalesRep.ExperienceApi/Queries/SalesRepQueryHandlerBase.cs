using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepQueryHandlerBase
{
    protected SalesRepQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
    {
        OrganizationAccessService = organizationAccessService;
    }

    protected ISalesRepOrganizationAccessService OrganizationAccessService { get; }

    /// <summary>
    /// Whether the caller is a sales rep at all, ignoring WHICH organizations they serve. Most surfaces want the
    /// narrowed <see cref="ISalesRepOrganizationAccessService.GetVisibleOrganizationIdsAsync"/> instead; this is for
    /// the ones whose data is personal rather than organization-scoped. Uses the same membership lookup either way,
    /// so a rep whose memberships are all locked still gets nothing.
    /// </summary>
    protected virtual async Task<bool> IsSalesRepAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var memberships = await OrganizationAccessService.GetGrantingMembershipsAsync([userId]);

        return memberships.Count > 0;
    }
}
