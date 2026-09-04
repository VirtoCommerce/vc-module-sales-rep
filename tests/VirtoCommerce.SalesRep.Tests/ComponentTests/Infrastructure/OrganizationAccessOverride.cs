using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

// A project override of the module's membership rule: the real service, minus one organization. Every other
// method on the access service - and both order surfaces - resolve through GetGrantingMembershipsAsync, so
// overriding it here is the single seam a project would use.
internal sealed class OrganizationAccessOverride : SalesRepOrganizationAccessService
{
    public const string HiddenOrganizationId = "org-hidden";

    public OrganizationAccessOverride(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
        : base(roleResolver, membershipSearchService)
    {
    }

    public override async Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(
        IList<string> userIds = null,
        IList<string> organizationIds = null)
    {
        var memberships = await base.GetGrantingMembershipsAsync(userIds, organizationIds);

        return memberships.Where(x => x.OrganizationId != HiddenOrganizationId).ToList();
    }
}
