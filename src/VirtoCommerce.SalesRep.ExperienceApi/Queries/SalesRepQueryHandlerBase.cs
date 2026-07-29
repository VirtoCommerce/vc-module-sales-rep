using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public abstract class SalesRepQueryHandlerBase
{
    private readonly ISalesRepOrganizationAccessService _organizationAccessService;

    protected SalesRepQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
    {
        _organizationAccessService = organizationAccessService;
    }

    protected Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(
        IList<string> userIds = null,
        IList<string> organizationIds = null)
        => _organizationAccessService.GetGrantingMembershipsAsync(userIds, organizationIds);

    protected Task<bool> ServesOrganizationAsync(string userId, string organizationId)
        => _organizationAccessService.ServesOrganizationAsync(userId, organizationId);

    protected Task<IList<string>> GetServedOrganizationIdsAsync(string userId)
        => _organizationAccessService.GetServedOrganizationIdsAsync(userId);

    protected Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId)
        => _organizationAccessService.GetVisibleOrganizationIdsAsync(userId, organizationId);

    protected Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId)
        => _organizationAccessService.GetVisibleGrantingMembershipsAsync(userId, organizationId);
}
