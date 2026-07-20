using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Default recipient policy for VCST-5310: every contact that belongs to the customer organization. This is the
/// out-of-the-box behavior mandated by the story ("all this customer's members get email / push"). Registered by
/// the module; a project swaps in <see cref="PrimaryContactRecipientResolver"/> (or its own) with a later DI
/// registration.
/// </summary>
public class AllMembersRecipientResolver : ISalesRepRecipientResolver
{
    private readonly IMemberSearchService _memberSearchService;

    public AllMembersRecipientResolver(IMemberSearchService memberSearchService)
    {
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<IList<Member>> ResolveRecipientsAsync(string organizationId, string responseGroup)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            return [];
        }

        var criteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        criteria.MemberId = organizationId;    // contacts belonging to the organization
        criteria.MemberType = nameof(Contact); // people only, not nested sub-organizations
        criteria.DeepSearch = false;           // this customer's own members (matches the salesRepCustomer detail rule)
        criteria.ResponseGroup = responseGroup;

        return await _memberSearchService.SearchAllAsync(criteria);
    }
}
