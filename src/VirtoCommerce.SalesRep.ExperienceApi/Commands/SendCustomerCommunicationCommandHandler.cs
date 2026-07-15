using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.PushMessages.Core.Services;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

/// <summary>
/// Sends a Sales Rep's communication to a customer organization's members over push and/or email (VCST-5310 /
/// VCST-5331). Recipients are resolved ONCE via <see cref="ISalesRepRecipientResolver"/> and both channels are fed
/// from that same set, so the audience is identical regardless of which channels are selected.
/// <para>
/// Access is gated by the module's single-sourced rule (inherited from <see cref="SalesRepQueryHandlerBase"/>):
/// the caller must hold an active sales-rep-granting membership in the target organization. Reusing that base —
/// rather than re-deriving the rule here — is what keeps this handler from drifting on "who may message an org".
/// </para>
/// </summary>
public class SendCustomerCommunicationCommandHandler
    : SalesRepQueryHandlerBase, IRequestHandler<SendCustomerCommunicationCommand, bool>
{
    private const int MaxMessageLength = 1000;

    private readonly ISalesRepRecipientResolver _recipientResolver;
    private readonly IPushMessageService _pushMessageService;
    private readonly INotificationSearchService _notificationSearchService;
    private readonly INotificationSender _notificationSender;
    private readonly IStoreService _storeService;

    public SendCustomerCommunicationCommandHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRecipientResolver recipientResolver,
        IPushMessageService pushMessageService,
        INotificationSearchService notificationSearchService,
        INotificationSender notificationSender,
        IStoreService storeService)
        : base(roleResolver, membershipSearchService)
    {
        _recipientResolver = recipientResolver;
        _pushMessageService = pushMessageService;
        _notificationSearchService = notificationSearchService;
        _notificationSender = notificationSender;
        _storeService = storeService;
    }

    public virtual async Task<bool> Handle(SendCustomerCommunicationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.OrganizationId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ExecutionError("Message is required.");
        }

        if (request.Message.Length > MaxMessageLength)
        {
            throw new ExecutionError($"Message must not exceed {MaxMessageLength} characters.");
        }

        // Nothing selected — no-op rather than an error, so a mis-toggled UI doesn't surface as a failure.
        if (!request.SendPush && !request.SendEmail)
        {
            return false;
        }

        // Access: the caller must serve exactly this organization (active, unlocked granting membership).
        var memberships = await GetGrantingMembershipsAsync([request.UserId], [request.OrganizationId]);
        if (memberships.Count == 0)
        {
            return false;
        }

        // Resolve the audience once; both channels use the same set.
        var recipients = await _recipientResolver.ResolveRecipientsAsync(request.OrganizationId);
        if (recipients.Count == 0)
        {
            return false;
        }

        if (request.SendPush)
        {
            await SendPushAsync(request, recipients);
        }

        if (request.SendEmail)
        {
            await SendEmailAsync(request, recipients);
        }

        return true;
    }

    /// <summary>
    /// Creates a single push message addressed to the resolved members (as a message with status <c>Sent</c>). The
    /// PushMessages module's event pipeline expands each member into its login accounts and delivers the push.
    /// The resolved member ids are passed directly (not the organization id) so the audience matches the email
    /// channel exactly, instead of the push job re-expanding the whole organization.
    /// </summary>
    protected virtual async Task SendPushAsync(SendCustomerCommunicationCommand request, IList<Member> recipients)
    {
        var pushMessage = AbstractTypeFactory<PushMessage>.TryCreateInstance();
        pushMessage.Topic = request.Title;
        pushMessage.ShortMessage = request.Message;
        pushMessage.Status = PushMessageStatus.Sent;
        pushMessage.MemberIds = recipients.Select(x => x.Id).ToList();

        await _pushMessageService.SaveChangesAsync([pushMessage]);
    }

    /// <summary>
    /// Sends one store-scoped email per resolved member that has an email address. The sender is taken from the
    /// store; the template is resolved for the store tenant and localized by <see cref="SendCustomerCommunicationCommand.CultureName"/>.
    /// </summary>
    protected virtual async Task SendEmailAsync(SendCustomerCommunicationCommand request, IList<Member> recipients)
    {
        // The template is identical for every recipient (only the To address differs), so resolve it once and
        // clone per recipient — rather than hitting the notification search service inside the loop.
        var template = await _notificationSearchService.GetNotificationAsync<SalesRepMessageEmailNotification>(
            new TenantIdentity(request.StoreId, nameof(Store)));

        if (template == null)
        {
            return;
        }

        var store = await _storeService.GetByIdAsync(request.StoreId);

        template.From = store?.Email;
        template.Title = request.Title;
        template.Message = request.Message;
        template.LanguageCode = request.CultureName;

        foreach (var recipient in recipients)
        {
            var email = recipient.Emails?.FirstOrDefault(x => !string.IsNullOrEmpty(x));
            if (string.IsNullOrEmpty(email))
            {
                continue;
            }

            var notification = (SalesRepMessageEmailNotification)template.Clone();
            notification.To = email;

            await _notificationSender.ScheduleSendNotificationAsync(notification);
        }
    }
}
