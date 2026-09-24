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

    // Is the caller a rep at all, ignoring WHICH organizations they serve. Surfaces whose data is
    // organization-scoped want GetVisibleOrganizationIdsAsync instead.
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
