using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
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
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;
    private readonly ISalesRepPrimaryContactResolver _primaryContactResolver;

    public SalesRepCustomerQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberService memberService,
        ISalesRepMemberResponseGroupParser responseGroupParser,
        ISalesRepPrimaryContactResolver primaryContactResolver)
        : base(roleResolver, membershipSearchService)
    {
        _memberService = memberService;
        _responseGroupParser = responseGroupParser;
        _primaryContactResolver = primaryContactResolver;
    }

    public virtual async Task<SalesRepCustomerDetails> Handle(SalesRepCustomerQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        if (!await ServesOrganizationAsync(request.UserId, request.OrganizationId))
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
            primaryContact = await _primaryContactResolver.ResolvePrimaryContactAsync(organization, _contactResponseGroup);
        }

        return SalesRepCustomerDetails.FromOrganization(organization, primaryContact);
    }
}
