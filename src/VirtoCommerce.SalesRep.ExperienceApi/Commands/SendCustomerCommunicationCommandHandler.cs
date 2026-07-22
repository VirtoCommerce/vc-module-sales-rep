using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.PushMessages.Core.Services;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SendCustomerCommunicationCommandHandler
    : SalesRepQueryHandlerBase, IRequestHandler<SendCustomerCommunicationCommand, bool>
{
    private const int MaxMessageLength = 1000;

    private readonly ISalesRepRecipientResolver _recipientResolver;
    private readonly ISalesRepCommunicationResponseGroupParser _responseGroupParser;
    private readonly IPushMessageService _pushMessageService;
    private readonly INotificationSearchService _notificationSearchService;
    private readonly INotificationSender _notificationSender;
    private readonly IStoreService _storeService;
    private readonly IUserSearchService _userSearchService;
    private readonly ILogger<SendCustomerCommunicationCommandHandler> _logger;

    public SendCustomerCommunicationCommandHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepRecipientResolver recipientResolver,
        ISalesRepCommunicationResponseGroupParser responseGroupParser,
        IPushMessageService pushMessageService,
        INotificationSearchService notificationSearchService,
        INotificationSender notificationSender,
        IStoreService storeService,
        IUserSearchService userSearchService,
        ILogger<SendCustomerCommunicationCommandHandler> logger)
        : base(roleResolver, membershipSearchService)
    {
        _recipientResolver = recipientResolver;
        _responseGroupParser = responseGroupParser;
        _pushMessageService = pushMessageService;
        _notificationSearchService = notificationSearchService;
        _notificationSender = notificationSender;
        _storeService = storeService;
        _userSearchService = userSearchService;
        _logger = logger;
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

        if (!request.SendPush && !request.SendEmail)
        {
            return false;
        }

        if (!await ServesOrganizationAsync(request.UserId, request.OrganizationId))
        {
            return false;
        }

        var responseGroup = _responseGroupParser.GetResponseGroup(request);
        var recipients = await _recipientResolver.ResolveRecipientsAsync(request.OrganizationId, responseGroup);

        recipients = await ExcludeInitiatorAsync(recipients, request.UserId);

        if (recipients.Count == 0)
        {
            return false;
        }

        var dispatched = false;

        if (request.SendPush)
        {
            dispatched |= await TryDispatchAsync(() => SendPushAsync(request, recipients), "push", request.OrganizationId);
        }

        if (request.SendEmail)
        {
            dispatched |= await TryDispatchAsync(() => SendEmailAsync(request, recipients), "email", request.OrganizationId);
        }

        return dispatched;
    }

    protected virtual async Task<bool> TryDispatchAsync(Func<Task> dispatch, string channel, string organizationId)
    {
        try
        {
            await dispatch();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sales Rep {Channel} communication to organization {OrganizationId} failed.", channel, organizationId);
            return false;
        }
    }

    protected virtual async Task<IList<Member>> ExcludeInitiatorAsync(IList<Member> recipients, string userId)
    {
        if (recipients.Count == 0)
        {
            return recipients;
        }

        var criteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = [userId];
        criteria.Take = 1;

        var user = (await _userSearchService.SearchUsersAsync(criteria)).Results.FirstOrDefault();
        var memberId = user?.MemberId;

        return string.IsNullOrEmpty(memberId)
            ? recipients
            : recipients.Where(x => !string.Equals(x.Id, memberId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    protected virtual async Task SendPushAsync(SendCustomerCommunicationCommand request, IList<Member> recipients)
    {
        var pushMessage = AbstractTypeFactory<PushMessage>.TryCreateInstance();
        pushMessage.Topic = request.Title;
        pushMessage.ShortMessage = request.Message;
        pushMessage.Status = PushMessageStatus.Sent;
        pushMessage.MemberIds = recipients.Select(x => x.Id).ToList();

        await _pushMessageService.SaveChangesAsync([pushMessage]);
    }

    protected virtual async Task SendEmailAsync(SendCustomerCommunicationCommand request, IList<Member> recipients)
    {
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
