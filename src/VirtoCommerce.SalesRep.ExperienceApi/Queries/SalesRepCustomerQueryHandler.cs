using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerQuery, SalesRepCustomerDetails>
{
    private static readonly string _contactResponseGroup =
        (MemberResponseGroup.WithPhones | MemberResponseGroup.WithEmails).ToString();

    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;

    public SalesRepCustomerQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberService memberService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser)
        : base(roleResolver, membershipSearchService)
    {
        _memberService = memberService;
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
    }

    public virtual async Task<SalesRepCustomerDetails> Handle(SalesRepCustomerQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        var memberships = await GetGrantingMembershipsAsync(
            [request.UserId],
            [request.OrganizationId]);

        if (memberships.Count == 0)
        {
            return null;
        }

        var organizationResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        var organization = (await _memberService.GetByIdsAsync(
                [request.OrganizationId],
                organizationResponseGroup,
                [nameof(Organization)]))
            .OfType<Organization>()
            .FirstOrDefault();

        if (organization == null)
        {
            return null;
        }

        Contact primaryContact = null;
        if (request.IncludeFields.IncludesField(nameof(SalesRepCustomerDetails.PrimaryContact))
            || request.IncludeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone)))
        {
            primaryContact = await ResolvePrimaryContactAsync(organization);
        }

        return SalesRepCustomerDetails.FromOrganization(organization, primaryContact);
    }

    private async Task<Contact> ResolvePrimaryContactAsync(Organization organization)
    {
        if (!string.IsNullOrEmpty(organization.OwnerId))
        {
            var owner = (await _memberService.GetByIdsAsync(
                    [organization.OwnerId],
                    _contactResponseGroup,
                    [nameof(Contact)]))
                .OfType<Contact>()
                .FirstOrDefault();

            if (owner != null)
            {
                return owner;
            }
        }

        var contactsCriteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        contactsCriteria.MemberId = organization.Id;
        contactsCriteria.MemberType = nameof(Contact);
        contactsCriteria.DeepSearch = false;
        contactsCriteria.ResponseGroup = _contactResponseGroup;
        contactsCriteria.Sort = "createdDate:asc";
        contactsCriteria.Take = 1;
        var contactsSearchResult = await _memberSearchService.SearchMembersAsync(contactsCriteria);

        return contactsSearchResult.Results.OfType<Contact>().FirstOrDefault();
    }
}
