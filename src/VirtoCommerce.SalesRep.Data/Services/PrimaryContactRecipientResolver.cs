using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class PrimaryContactRecipientResolver : ISalesRepRecipientResolver
{
    private readonly IMemberService _memberService;
    private readonly ISalesRepPrimaryContactResolver _primaryContactResolver;

    public PrimaryContactRecipientResolver(
        IMemberService memberService,
        ISalesRepPrimaryContactResolver primaryContactResolver)
    {
        _memberService = memberService;
        _primaryContactResolver = primaryContactResolver;
    }

    public virtual async Task<IList<Member>> ResolveRecipientsAsync(string organizationId, string responseGroup)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            return [];
        }

        var organization = (await _memberService.GetByIdsAsync(
                [organizationId],
                nameof(MemberResponseGroup.Default),
                [nameof(Organization)]))
            .OfType<Organization>()
            .FirstOrDefault();

        var primaryContact = await _primaryContactResolver.ResolvePrimaryContactAsync(organization, responseGroup);

        return primaryContact == null ? [] : [primaryContact];
    }
}
