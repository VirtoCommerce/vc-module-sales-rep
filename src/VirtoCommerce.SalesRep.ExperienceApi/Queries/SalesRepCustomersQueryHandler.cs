using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomersQuery, SalesRepCustomerSearchResult>
{
    private readonly IMemberSearchService _memberSearchService;

    public SalesRepCustomersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberSearchService memberSearchService)
        : base(roleResolver, membershipSearchService)
    {
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<SalesRepCustomerSearchResult> Handle(SalesRepCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = new SalesRepCustomerSearchResult();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        // All organizations where the caller holds a sales-rep-granting membership
        // (bounded by the rep's served-organization count).
        // OnlyUnlocked: a rep locked in an organization does not see it as a customer.
        var memberships = await GetGrantingMembershipsAsync(userIds: new[] { request.UserId });

        var organizationIds = memberships
            .Select(x => x.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        if (organizationIds.Length == 0)
        {
            return result;
        }

        // Filter (keyword by organization name), sort and page the organizations in the database.
        var membersSearchResult = await _memberSearchService.SearchMembersAsync(new MembersSearchCriteria
        {
            ObjectIds = organizationIds,
            MemberType = nameof(Organization),
            RootMembersOnly = false,
            // Only Id + Name are projected onto SalesRepCustomer (both scalar columns); skip collection loads.
            ResponseGroup = MemberResponseGroup.Default.ToString(),
            Keyword = request.Keyword,
            Sort = request.Sort,
            Skip = request.Skip,
            Take = request.Take,
        });

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .Select(x => new SalesRepCustomer
            {
                OrganizationId = x.Id,
                OrganizationName = x.Name,
                // Carry the caller's store so the lastOrder resolver can scope orders to it.
                StoreId = request.StoreId,
            })
            .ToList();

        return result;
    }
}
