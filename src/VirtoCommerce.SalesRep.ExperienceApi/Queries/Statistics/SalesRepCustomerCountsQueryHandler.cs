using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerCountsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerCountsQuery, SalesRepCustomerCountsContext>
{
    public SalesRepCustomerCountsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
        : base(roleResolver, membershipSearchService)
    {
    }

    public virtual async Task<SalesRepCustomerCountsContext> Handle(SalesRepCustomerCountsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        var memberships = await GetVisibleGrantingMembershipsAsync(request.UserId, request.OrganizationId);
        if (memberships.Count == 0)
        {
            return null;
        }

        var assignmentsByOrganization = memberships
            .Where(x => !string.IsNullOrEmpty(x.OrganizationId))
            .GroupBy(x => x.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, AssignedDate = g.Min(x => x.CreatedDate) })
            .ToArray();

        var result = AbstractTypeFactory<SalesRepCustomerCountsContext>.TryCreateInstance();
        result.OrganizationIds = assignmentsByOrganization.Select(x => x.OrganizationId).ToList();
        result.AssignmentDates = assignmentsByOrganization.Select(x => x.AssignedDate).ToList();
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        return result;
    }
}
