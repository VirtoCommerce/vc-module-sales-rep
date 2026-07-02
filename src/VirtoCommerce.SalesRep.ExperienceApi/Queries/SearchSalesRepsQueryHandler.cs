using System.Collections.Generic;
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

public class SearchSalesRepsQueryHandler : IQueryHandler<SearchSalesRepsQuery, SalesRepContactSearchResult>
{
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly IUserSearchService _userSearchService;
    private readonly IMemberSearchService _memberSearchService;

    public SearchSalesRepsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IUserSearchService userSearchService,
        IMemberSearchService memberSearchService)
    {
        _roleResolver = roleResolver;
        _membershipSearchService = membershipSearchService;
        _userSearchService = userSearchService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<SalesRepContactSearchResult> Handle(SearchSalesRepsQuery request, CancellationToken cancellationToken)
    {
        var result = new SalesRepContactSearchResult();

        if (string.IsNullOrEmpty(request.OrganizationId))
        {
            return result;
        }

        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return result;
        }

        // Users holding a sales-rep-granting role in the caller's organization. GetCountsByUserAsync groups
        // memberships by user in the database; its keys are exactly the users serving that organization.
        var repUserCounts = await _membershipSearchService.GetCountsByUserAsync(new OrganizationMembershipSearchCriteria
        {
            OrganizationIds = new[] { request.OrganizationId },
            RoleIds = grantingRoleIds.ToArray(),
        });

        var userIds = repUserCounts.Keys.ToArray();
        if (userIds.Length == 0)
        {
            return result;
        }

        // Map security accounts to their contact member ids.
        var users = await _userSearchService.SearchUsersAsync(new UserSearchCriteria
        {
            ObjectIds = userIds,
            Take = userIds.Length,
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
            ResponseGroup = MemberResponseGroup.Full.ToString(),
            Keyword = request.Keyword,
            Sort = request.Sort,
            Skip = request.Skip,
            Take = request.Take,
        });

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .OfType<Contact>()
            .Select(MapContact)
            .ToList();

        return result;
    }

    private static SalesRepContact MapContact(Contact contact)
    {
        return new SalesRepContact
        {
            Id = contact.Id,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            MiddleName = contact.MiddleName,
            FullName = contact.FullName,
            Name = contact.Name,
            About = contact.About,
            PhotoUrl = contact.PhotoUrl,
            Emails = contact.Emails ?? new List<string>(),
            Phones = contact.Phones ?? new List<string>(),
        };
    }
}
