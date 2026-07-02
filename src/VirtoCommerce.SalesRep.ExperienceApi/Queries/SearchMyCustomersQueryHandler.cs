using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SearchMyCustomersQueryHandler : IQueryHandler<SearchMyCustomersQuery, SalesRepCustomerSearchResult>
{
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly IMemberSearchService _memberSearchService;

    public SearchMyCustomersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberSearchService memberSearchService)
    {
        _roleResolver = roleResolver;
        _membershipSearchService = membershipSearchService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<SalesRepCustomerSearchResult> Handle(SearchMyCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = new SalesRepCustomerSearchResult();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return result;
        }

        // All organizations where the caller holds a sales-rep-granting membership
        // (bounded by the rep's served-organization count).
        var memberships = await _membershipSearchService.SearchAllNoCloneAsync(new OrganizationMembershipSearchCriteria
        {
            UserIds = new[] { request.UserId },
            RoleIds = grantingRoleIds.ToArray(),
        });

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
            })
            .ToList();

        return result;
    }
}
