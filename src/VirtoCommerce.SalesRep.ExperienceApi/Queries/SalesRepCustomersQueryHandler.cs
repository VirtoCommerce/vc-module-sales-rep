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
        var result = AbstractTypeFactory<SalesRepCustomerSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        // The organizations this rep serves (bounded by the rep's served-organization count).
        // OnlyUnlocked: a rep locked in an organization does not see it as a customer.
        var organizationIds = await GetServedOrganizationIdsAsync(request.UserId);

        if (organizationIds.Length == 0)
        {
            return result;
        }

        // Filter (keyword by organization name), sort and page the organizations in the database.
        // GetSearchCriteria carries the request's Keyword/Sort/Skip/Take onto the criteria.
        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = organizationIds;
        membersCriteria.MemberType = nameof(Organization);
        membersCriteria.RootMembersOnly = false;
        // Only Id + Name are projected onto SalesRepCustomer (both scalar columns); skip collection loads.
        membersCriteria.ResponseGroup = MemberResponseGroup.Default.ToString();
        var membersSearchResult = await _memberSearchService.SearchMembersAsync(membersCriteria);

        result.TotalCount = membersSearchResult.TotalCount;
        // Carry the caller's store onto each row so the lastOrder resolver can scope orders to it.
        result.Results = membersSearchResult.Results
            .Select(x => SalesRepCustomer.FromOrganization(x, request.StoreId))
            .ToList();

        return result;
    }
}
