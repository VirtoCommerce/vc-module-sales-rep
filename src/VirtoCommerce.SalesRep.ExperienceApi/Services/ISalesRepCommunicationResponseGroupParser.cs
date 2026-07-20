using VirtoCommerce.SalesRep.ExperienceApi.Commands;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Translates a customer-communication request into the minimal <c>MemberResponseGroup</c> needed to hydrate the
/// recipients for the selected channels — the email channel needs the <c>Emails</c> collection; push needs only the
/// member id (<c>Default</c>). Mirrors <see cref="ISalesRepMemberResponseGroupParser"/> and
/// <see cref="ISalesRepOrderResponseGroupParser"/>; a project overrides it (last registration wins) to change what
/// recipient data each channel loads.
/// </summary>
public interface ISalesRepCommunicationResponseGroupParser
{
    /// <returns>A <c>MemberResponseGroup</c> flags string for the recipient member load.</returns>
    string GetResponseGroup(SendCustomerCommunicationCommand command);
}
