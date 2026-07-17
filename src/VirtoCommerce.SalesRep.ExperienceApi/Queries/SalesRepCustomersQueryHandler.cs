using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomersQuery, SalesRepCustomerSearchResult>
{
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;

    public SalesRepCustomersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
        _filterRuleResolver = filterRuleResolver;
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
        // Load only the member data the caller selected — the organization's addresses only when `address` was
        // requested (id/name/iconUrl are scalar columns loaded with Default). Mirrors the order query's field-driven
        // response group so the list never over-fetches.
        membersCriteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        // Apply the selected customer segment via the shared resolver (same rule the counts use). Null means a
        // segment name was given but is unrecognized — return an empty result rather than every served customer.
        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, membersCriteria);
        if (filteredCriteria == null)
        {
            return result;
        }

        var membersSearchResult = await _memberSearchService.SearchMembersAsync(filteredCriteria);

        result.TotalCount = membersSearchResult.TotalCount;
        // Carry the caller's store onto each row so the lastOrder resolver can scope orders to it.
        result.Results = membersSearchResult.Results
            .Select(x => SalesRepCustomer.FromOrganization(x, request.StoreId))
            .ToList();

        return result;
    }
}
