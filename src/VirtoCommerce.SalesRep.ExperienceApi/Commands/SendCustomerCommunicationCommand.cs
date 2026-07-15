using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

/// <summary>
/// A Sales Rep sends a communication (push notification and/or email) to a customer organization's members
/// (VCST-5310 / VCST-5331). The Rep is the caller; <see cref="UserId"/> is set server-side from their claims and
/// the handler verifies the Rep actually serves <see cref="OrganizationId"/> before sending.
/// </summary>
public class SendCustomerCommunicationCommand : ICommand<bool>
{
    /// <summary>Customer organization whose members receive the message.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Send an in-store push notification to the recipients.</summary>
    public bool SendPush { get; set; }

    /// <summary>Send an email to the recipients.</summary>
    public bool SendEmail { get; set; }

    /// <summary>Optional message title/heading.</summary>
    public string Title { get; set; }

    /// <summary>The Rep's message (required, max 1000 chars). May contain a URL.</summary>
    public string Message { get; set; }

    /// <summary>Store the message is sent on behalf of — scopes the email template and sender address.</summary>
    public string StoreId { get; set; }

    /// <summary>Optional culture for localizing the email template (e.g. "en-US").</summary>
    public string CultureName { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }
}
