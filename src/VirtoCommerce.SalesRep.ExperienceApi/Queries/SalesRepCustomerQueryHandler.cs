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

public class SalesRepCustomerQueryHandler : IQueryHandler<SalesRepCustomerQuery, SalesRepCustomerDetails>
{
    private static readonly string _contactResponseGroup =
        (MemberResponseGroup.WithPhones | MemberResponseGroup.WithEmails).ToString();

    private static readonly string _organizationResponseGroup =
        (MemberResponseGroup.WithAddresses | MemberResponseGroup.WithPhones | MemberResponseGroup.WithEmails).ToString();

    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;
    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;

    public SalesRepCustomerQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberService memberService,
        IMemberSearchService memberSearchService)
    {
        _roleResolver = roleResolver;
        _membershipSearchService = membershipSearchService;
        _memberService = memberService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<SalesRepCustomerDetails> Handle(SalesRepCustomerQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return null;
        }

        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return null;
        }

        // Security scoping: the caller must hold an active sales-rep-granting membership in exactly the
        // requested organization. Without this a rep could read any organization by guessing its id.
        // OnlyUnlocked: a rep locked in an organization must not see it as a customer.
        var memberships = await _membershipSearchService.SearchAllNoCloneAsync(new OrganizationMembershipSearchCriteria
        {
            UserIds = new[] { request.UserId },
            OrganizationIds = new[] { request.OrganizationId },
            RoleIds = grantingRoleIds.ToArray(),
            OnlyUnlocked = true,
        });

        if (memberships.Count == 0)
        {
            return null;
        }

        var organization = (await _memberService.GetByIdsAsync(
                new[] { request.OrganizationId },
                _organizationResponseGroup,
                new[] { nameof(Organization) }))
            .OfType<Organization>()
            .FirstOrDefault();

        if (organization == null)
        {
            return null;
        }

        var result = new SalesRepCustomerDetails
        {
            OrganizationId = organization.Id,
            OrganizationName = organization.Name,
            AccountType = organization.BusinessCategory,
            ShipTo = FormatShipTo(organization),
        };

        var primaryContact = await ResolvePrimaryContactAsync(organization);
        if (primaryContact != null)
        {
            result.PrimaryContact = SalesRepContact.FromContact(primaryContact);
        }

        // Phone: the primary contact's first, falling back to the organization's.
        result.Phone = primaryContact?.Phones?.FirstOrDefault() ?? organization.Phones?.FirstOrDefault();

        return result;
    }

    /// <summary>
    /// Resolves the organization's primary contact: its owner, then the first contact member as a fallback.
    /// </summary>
    private async Task<Contact> ResolvePrimaryContactAsync(Organization organization)
    {
        if (!string.IsNullOrEmpty(organization.OwnerId))
        {
            var owner = (await _memberService.GetByIdsAsync(
                    new[] { organization.OwnerId },
                    _contactResponseGroup,
                    new[] { nameof(Contact) }))
                .OfType<Contact>()
                .FirstOrDefault();

            if (owner != null)
            {
                return owner;
            }
        }

        // Fallback: the first (oldest) contact directly belonging to the organization.
        var contactsSearchResult = await _memberSearchService.SearchMembersAsync(new MembersSearchCriteria
        {
            MemberId = organization.Id,
            MemberType = nameof(Contact),
            DeepSearch = false,
            ResponseGroup = _contactResponseGroup,
            Sort = "createdDate:asc",
            Take = 1,
        });

        return contactsSearchResult.Results.OfType<Contact>().FirstOrDefault();
    }

    private static string FormatShipTo(Organization organization)
    {
        var address = organization.Addresses?.FirstOrDefault(x => x.IsDefault)
            ?? organization.Addresses?.FirstOrDefault();

        if (address == null)
        {
            return null;
        }

        var parts = new[] { address.City, address.RegionName }.Where(x => !string.IsNullOrWhiteSpace(x));
        var shipTo = string.Join(", ", parts);

        return string.IsNullOrEmpty(shipTo) ? null : shipTo;
    }
}
