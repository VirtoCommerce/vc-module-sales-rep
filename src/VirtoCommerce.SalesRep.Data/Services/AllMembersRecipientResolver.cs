using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

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
        criteria.MemberId = organizationId;
        criteria.MemberType = nameof(Contact);
        criteria.DeepSearch = false;
        criteria.ResponseGroup = responseGroup;

        return await _memberSearchService.SearchAllAsync(criteria);
    }
}
