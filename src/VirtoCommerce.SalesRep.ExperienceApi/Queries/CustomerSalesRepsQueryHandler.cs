using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class CustomerSalesRepsQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<CustomerSalesRepsQuery, SalesRepContactSearchResult>
{
    private readonly IUserSearchService _userSearchService;
    private readonly IMemberSearchService _memberSearchService;

    public CustomerSalesRepsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IUserSearchService userSearchService,
        IMemberSearchService memberSearchService)
        : base(roleResolver, membershipSearchService)
    {
        _userSearchService = userSearchService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<SalesRepContactSearchResult> Handle(CustomerSalesRepsQuery request, CancellationToken cancellationToken)
    {
        var result = new SalesRepContactSearchResult();

        if (string.IsNullOrEmpty(request.OrganizationId))
        {
            return result;
        }

        // Memberships carrying a sales-rep-granting role in the caller's organization. OnlyUnlocked excludes
        // per-org locked memberships (a rep locked in this organization must not appear for it).
        var memberships = await GetGrantingMembershipsAsync(organizationIds: new[] { request.OrganizationId });

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
        var users = await _userSearchService.SearchUsersAsync(new UserSearchCriteria
        {
            ObjectIds = userIds,
            Take = userIds.Length,
            OnlyUnlocked = true,
        });

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
        var membersSearchResult = await _memberSearchService.SearchMembersAsync(new MembersSearchCriteria
        {
            ObjectIds = memberIds,
            MemberType = nameof(Contact),
            RootMembersOnly = false,
            // Only emails + phones are projected onto SalesRepContact; skip the rest of the Full graph.
            ResponseGroup = (MemberResponseGroup.WithEmails | MemberResponseGroup.WithPhones).ToString(),
            Keyword = request.Keyword,
            Sort = request.Sort,
            Skip = request.Skip,
            Take = request.Take,
        });

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .OfType<Contact>()
            .Select(SalesRepContact.FromContact)
            .ToList();

        return result;
    }
}
