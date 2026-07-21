using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// Builds the "my customers" counts context for the current Sales Rep: resolves the organizations they serve and the
/// per-organization assignment dates (from their granting memberships), so the counters (ordering / new customers)
/// can be computed per date range. Returns null when the caller has no identity or serves no organizations.
/// </summary>
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

        // Membership scoping: the one requested customer (only if the rep serves it), or every organization the rep
        // is assigned to. Empty means the rep serves none → no counts.
        var memberships = await GetVisibleGrantingMembershipsAsync(request.UserId, request.OrganizationId);
        if (memberships.Count == 0)
        {
            return null;
        }

        // The organizations the rep serves, and the date each was first assigned (earliest granting-membership per org):
        // "new customers" counts the assignments falling in a window, independent of orders (a long-standing customer
        // assigned recently is "new").
        var assignmentsByOrganization = memberships
            .Where(x => !string.IsNullOrEmpty(x.OrganizationId))
            .GroupBy(x => x.OrganizationId)
            .Select(g => new { OrganizationId = g.Key, AssignedDate = g.Min(x => x.CreatedDate) })
            .ToArray();

        var result = AbstractTypeFactory<SalesRepCustomerCountsContext>.TryCreateInstance();
        result.OrganizationIds = assignmentsByOrganization.Select(x => x.OrganizationId).ToArray();
        result.AssignmentDates = assignmentsByOrganization.Select(x => x.AssignedDate).ToArray();
        // Creator scoping: counters derive only from orders the rep created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        return result;
    }
}
