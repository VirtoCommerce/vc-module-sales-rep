using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Alternative recipient policy: only the organization's primary contact — its <c>OwnerId</c> contact, falling
/// back to the oldest contact member. Not registered by default; a project opts in with a DI registration after
/// the module's. Mirrors the primary-contact rule used by the <c>salesRepCustomer</c> detail query so both agree
/// on "who is the primary contact".
/// </summary>
public class PrimaryContactRecipientResolver : ISalesRepRecipientResolver
{
    private static readonly string _responseGroup = MemberResponseGroup.WithEmails.ToString();

    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;

    public PrimaryContactRecipientResolver(
        IMemberService memberService,
        IMemberSearchService memberSearchService)
    {
        _memberService = memberService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<IList<Member>> ResolveRecipientsAsync(string organizationId)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            return [];
        }

        var organization = (await _memberService.GetByIdsAsync(
                [organizationId],
                responseGroup: null,
                [nameof(Organization)]))
            .OfType<Organization>()
            .FirstOrDefault();

        if (organization == null)
        {
            return [];
        }

        var primaryContact = await ResolvePrimaryContactAsync(organization);

        return primaryContact == null ? [] : [primaryContact];
    }

    /// <summary>The organization's owner contact, then the oldest contact member as a fallback.</summary>
    protected virtual async Task<Contact> ResolvePrimaryContactAsync(Organization organization)
    {
        if (!string.IsNullOrEmpty(organization.OwnerId))
        {
            var owner = (await _memberService.GetByIdsAsync(
                    [organization.OwnerId],
                    _responseGroup,
                    [nameof(Contact)]))
                .OfType<Contact>()
                .FirstOrDefault();

            if (owner != null)
            {
                return owner;
            }
        }

        var criteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        criteria.MemberId = organization.Id;
        criteria.MemberType = nameof(Contact);
        criteria.DeepSearch = false;
        criteria.ResponseGroup = _responseGroup;
        criteria.Sort = "createdDate:asc";
        criteria.Take = 1;

        var searchResult = await _memberSearchService.SearchMembersAsync(criteria);

        return searchResult.Results.OfType<Contact>().FirstOrDefault();
    }
}
