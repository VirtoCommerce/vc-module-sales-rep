using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepPrimaryContactResolver : ISalesRepPrimaryContactResolver
{
    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepOrganizationAccessService _organizationAccessService;
    private readonly IUserSearchService _userSearchService;

    public SalesRepPrimaryContactResolver(
        IMemberService memberService,
        IMemberSearchService memberSearchService,
        ISalesRepOrganizationAccessService organizationAccessService,
        IUserSearchService userSearchService)
    {
        _memberService = memberService;
        _memberSearchService = memberSearchService;
        _organizationAccessService = organizationAccessService;
        _userSearchService = userSearchService;
    }

    public virtual async Task<Contact> ResolvePrimaryContactAsync(Organization organization, string responseGroup)
    {
        if (string.IsNullOrEmpty(organization?.Id))
        {
            return null;
        }

        var primaryContacts = await ResolvePrimaryContactsAsync([organization], responseGroup);

        return primaryContacts.TryGetValue(organization.Id, out var primaryContact) ? primaryContact : null;
    }

    // The owner when it exists, else the organization's first contact member that is not one of its sales reps.
    public virtual async Task<IDictionary<string, Contact>> ResolvePrimaryContactsAsync(IList<Organization> organizations, string responseGroup)
    {
        var result = new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);

        var candidates = organizations
            .Where(x => !string.IsNullOrEmpty(x?.Id))
            .ToList();
        if (candidates.Count == 0)
        {
            return result;
        }

        var ownersById = await GetOwnersAsync(candidates, responseGroup);

        var withoutOwner = new List<Organization>();
        foreach (var organization in candidates)
        {
            if (!string.IsNullOrEmpty(organization.OwnerId) && ownersById.TryGetValue(organization.OwnerId, out var owner))
            {
                result[organization.Id] = owner;
            }
            else
            {
                withoutOwner.Add(organization);
            }
        }

        if (withoutOwner.Count == 0)
        {
            return result;
        }

        var salesRepMemberIds = await GetSalesRepMemberIdsAsync(withoutOwner.Select(x => x.Id).ToList());

        foreach (var organization in withoutOwner)
        {
            var excludedMemberIds = salesRepMemberIds.TryGetValue(organization.Id, out var memberIds)
                ? memberIds
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var firstContact = await GetFirstContactAsync(organization, responseGroup, excludedMemberIds);
            if (firstContact != null)
            {
                result[organization.Id] = firstContact;
            }
        }

        return result;
    }

    protected virtual async Task<IDictionary<string, Contact>> GetOwnersAsync(IList<Organization> organizations, string responseGroup)
    {
        var ownerIds = organizations
            .Select(x => x.OwnerId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ownerIds.Length == 0)
        {
            return new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);
        }

        var owners = await _memberService.GetByIdsAsync(ownerIds, responseGroup, [nameof(Contact)]);

        return owners
            .OfType<Contact>()
            .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contact ids of the sales reps of each organization, keyed by organization id.
    /// </summary>
    protected virtual async Task<IDictionary<string, ISet<string>>> GetSalesRepMemberIdsAsync(IList<string> organizationIds)
    {
        var result = new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase);

        var memberships = await _organizationAccessService.GetGrantingMembershipsAsync(organizationIds: organizationIds);

        var userIds = memberships
            .Select(x => x.UserId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (userIds.Length == 0)
        {
            return result;
        }

        var userCriteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        userCriteria.ObjectIds = userIds;
        var users = await _userSearchService.SearchAllAsync(userCriteria);

        var memberIdByUserId = users
            .Where(x => !string.IsNullOrEmpty(x.MemberId))
            .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id, x => x.MemberId, StringComparer.OrdinalIgnoreCase);

        foreach (var membership in memberships)
        {
            if (string.IsNullOrEmpty(membership.OrganizationId)
                || string.IsNullOrEmpty(membership.UserId)
                || !memberIdByUserId.TryGetValue(membership.UserId, out var memberId))
            {
                continue;
            }

            if (!result.TryGetValue(membership.OrganizationId, out var memberIds))
            {
                memberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result[membership.OrganizationId] = memberIds;
            }

            memberIds.Add(memberId);
        }

        return result;
    }

    protected virtual async Task<Contact> GetFirstContactAsync(Organization organization, string responseGroup, ISet<string> excludedMemberIds)
    {
        var criteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        criteria.MemberId = organization.Id;
        criteria.MemberType = nameof(Contact);
        criteria.DeepSearch = false;
        criteria.ResponseGroup = responseGroup;
        criteria.Sort = "createdDate:asc";
        // Every excluded contact can come first, so one more than their count is enough to reach a customer contact.
        criteria.Take = excludedMemberIds.Count + 1;

        var searchResult = await _memberSearchService.SearchMembersAsync(criteria);

        return searchResult.Results
            .OfType<Contact>()
            .FirstOrDefault(x => !excludedMemberIds.Contains(x.Id));
    }
}
