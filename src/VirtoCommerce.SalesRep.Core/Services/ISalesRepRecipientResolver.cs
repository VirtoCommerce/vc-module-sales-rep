using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Resolves the members of a customer organization that should receive a Sales Rep's communication
/// (VCST-5310). This is the single seam that decides "who gets the message" — the command handler resolves
/// the set once and feeds BOTH channels (push + email) from it, so the recipients stay identical regardless
/// of which channels are selected.
/// <para>
/// The default implementation targets every member of the organization. A project can change the policy
/// (e.g. only the organization's primary/owner contact) by registering its own implementation AFTER the
/// module's registration — last registration wins, the same override pattern the module uses for
/// <c>ISalesRepOrderStatusService</c>.
/// </para>
/// This resolver does NOT enforce access: the caller must first verify the current Rep is authorized to
/// message the organization.
/// </summary>
public interface ISalesRepRecipientResolver
{
    /// <summary>
    /// The members (contacts) of <paramref name="organizationId"/> to address. Returned members are loaded
    /// with their emails so the email channel can read a recipient address; the push channel only needs their
    /// ids (it resolves each member's login accounts itself). Never null; empty when the org has no members.
    /// </summary>
    Task<IList<Member>> ResolveRecipientsAsync(string organizationId);
}
