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

        // Security scoping: the caller must hold an active sales-rep-granting membership in exactly the
        // requested organization. Without this a rep could read any organization by guessing its id.
        // OnlyUnlocked: a rep locked in an organization must not see it as a customer.
        var memberships = await GetGrantingMembershipsAsync(
            [request.UserId],
            [request.OrganizationId]);

        if (memberships.Count == 0)
        {
            return null;
        }

        // Load only the member data the caller selected — the organization's addresses only when `address` was
        // requested, its phones only when `phone` was (id/name/iconUrl/accountType are scalar, loaded with Default).
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

        // primaryContact is a separate lookup, so resolve it only when the caller selected it — or `phone`, which
        // falls back to the primary contact's phone. Mirrors the field-driven organization load above.
        Contact primaryContact = null;
        if (request.IncludeFields.IncludesField(nameof(SalesRepCustomerDetails.PrimaryContact))
            || request.IncludeFields.IncludesField(nameof(SalesRepCustomerDetails.Phone)))
        {
            primaryContact = await _primaryContactResolver.ResolvePrimaryContactAsync(organization, _contactResponseGroup);
        }

        return SalesRepCustomerDetails.FromOrganization(organization, primaryContact);
    }
}
