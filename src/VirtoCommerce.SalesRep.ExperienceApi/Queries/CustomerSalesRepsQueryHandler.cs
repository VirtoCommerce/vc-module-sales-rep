using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class CustomerSalesRepsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<CustomerSalesRepsQuery, SalesRepContactSearchResult>
{
    private readonly IUserSearchService _userSearchService;
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;

    public CustomerSalesRepsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IUserSearchService userSearchService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser)
        : base(roleResolver, membershipSearchService)
    {
        _userSearchService = userSearchService;
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
    }

    public virtual async Task<SalesRepContactSearchResult> Handle(CustomerSalesRepsQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepContactSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.OrganizationId))
        {
            return result;
        }

        // Memberships carrying a sales-rep-granting role in the caller's organization. OnlyUnlocked excludes
        // per-org locked memberships (a rep locked in this organization must not appear for it).
        var memberships = await GetGrantingMembershipsAsync(organizationIds: [request.OrganizationId]);

        var userIds = memberships
            .Select(m => m.UserId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();
        if (userIds.Length == 0)
        {
            return result;
        }

        // Map security accounts to contact member ids. OnlyUnlocked returns only active accounts (VCST-4907 #5):
        // blocked/disabled reps are excluded. Deleted reps have no membership and never reach here.
        // StoreId scopes to the caller's store when provided — a rep's account is store-bound, so a rep from
        // another store is not exposed to this storefront.
        var userCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        userCriteria.ObjectIds = userIds;
        userCriteria.Take = userIds.Length;
        userCriteria.OnlyUnlocked = true;
        userCriteria.StoreId = request.StoreId;
        var users = await _userSearchService.SearchUsersAsync(userCriteria);

        var memberIds = users.Results
            .Select(x => x.MemberId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToArray();

        if (memberIds.Length == 0)
        {
            return result;
        }

        // Filter (keyword), sort and page the reps' contacts in the database.
        // GetSearchCriteria carries the request's Keyword/Sort/Skip/Take onto the criteria.
        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = memberIds;
        membersCriteria.MemberType = nameof(Contact);
        membersCriteria.RootMembersOnly = false;
        // Load only the member data the caller selected — emails/phones only when those fields were requested
        // (id/name/photoUrl are scalar columns loaded with Default). Mirrors the customer queries' field-driven group.
        membersCriteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);
        var membersSearchResult = await _memberSearchService.SearchMembersAsync(membersCriteria);

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .OfType<Contact>()
            .Select(SalesRepContact.FromContact)
            .ToList();

        return result;
    }
}
