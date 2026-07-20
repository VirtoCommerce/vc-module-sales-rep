using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

/// <summary>
/// Alternative recipient policy: only the organization's primary contact (see
/// <see cref="ISalesRepPrimaryContactResolver"/> for the owner→oldest-contact rule). Not registered by default; a
/// project opts in with a DI registration after the module's. Delegating to the shared primary-contact resolver
/// keeps this policy and the <c>salesRepCustomer</c> detail card from drifting on "who is the primary contact".
/// </summary>
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

        // The organization is loaded only to resolve its primary contact (owner id, else oldest contact), so a
        // minimal group is enough; the contact itself is hydrated to the caller's responseGroup.
        var organization = (await _memberService.GetByIdsAsync(
                [organizationId],
                MemberResponseGroup.Default.ToString(),
                [nameof(Organization)]))
            .OfType<Organization>()
            .FirstOrDefault();

        var primaryContact = await _primaryContactResolver.ResolvePrimaryContactAsync(organization, responseGroup);

        return primaryContact == null ? [] : [primaryContact];
    }
}
