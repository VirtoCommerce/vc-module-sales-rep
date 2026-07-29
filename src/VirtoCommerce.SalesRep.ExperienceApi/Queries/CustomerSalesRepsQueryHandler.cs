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
        ISalesRepOrganizationAccessService organizationAccessService,
        IUserSearchService userSearchService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser)
        : base(organizationAccessService)
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

        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = memberIds;
        membersCriteria.MemberType = nameof(Contact);
        membersCriteria.RootMembersOnly = false;
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
