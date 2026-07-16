using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

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
        var organizationIds = await GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Length == 0)
        {
            return null;
        }

        var result = AbstractTypeFactory<SalesRepCustomerCountsContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        // Creator scoping: counters derive only from orders the rep created (data-isolation invariant).
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        return result;
    }
}
