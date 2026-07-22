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

    public SalesRepCustomersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser)
        : base(roleResolver, membershipSearchService)
    {
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
    }

    public virtual async Task<SalesRepCustomerSearchResult> Handle(SalesRepCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepCustomerSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var organizationIds = await GetServedOrganizationIdsAsync(request.UserId);

        if (organizationIds.Length == 0)
        {
            return result;
        }

        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = organizationIds;
        membersCriteria.MemberType = nameof(Organization);
        membersCriteria.RootMembersOnly = false;
        membersCriteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);
        var membersSearchResult = await _memberSearchService.SearchMembersAsync(membersCriteria);

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .Select(x => SalesRepCustomer.FromOrganization(x, request.StoreId))
            .ToList();

        return result;
    }
}
