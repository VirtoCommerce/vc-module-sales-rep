using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepPrimaryContactResolver : ISalesRepPrimaryContactResolver
{
    private readonly IMemberService _memberService;
    private readonly IMemberSearchService _memberSearchService;

    public SalesRepPrimaryContactResolver(
        IMemberService memberService,
        IMemberSearchService memberSearchService)
    {
        _memberService = memberService;
        _memberSearchService = memberSearchService;
    }

    public virtual async Task<Contact> ResolvePrimaryContactAsync(Organization organization, string responseGroup)
    {
        if (organization == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(organization.OwnerId))
        {
            var owner = (await _memberService.GetByIdsAsync(
                    [organization.OwnerId],
                    responseGroup,
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
        criteria.ResponseGroup = responseGroup;
        criteria.Sort = "createdDate:asc";
        criteria.Take = 1;

        var searchResult = await _memberSearchService.SearchMembersAsync(criteria);

        return searchResult.Results.OfType<Contact>().FirstOrDefault();
    }
}
