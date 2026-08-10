using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepOrganizationAccessService
{
    Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null);

    Task<bool> ServesOrganizationAsync(string userId, string organizationId);

    Task<IList<string>> GetServedOrganizationIdsAsync(string userId);

    Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId);

    Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId);
}
