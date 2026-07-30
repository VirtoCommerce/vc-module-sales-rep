using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.PushMessages.Core.Services;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class SendCustomerCommunicationCommandHandler
    : SalesRepQueryHandlerBase, IRequestHandler<SendCustomerCommunicationCommand, SalesRepCommunicationResult>
{
    private readonly ISalesRepRecipientResolver _recipientResolver;
    private readonly ISalesRepCommunicationResponseGroupParser _responseGroupParser;
    private readonly IPushMessageService _pushMessageService;
    private readonly INotificationSearchService _notificationSearchService;
    private readonly INotificationSender _notificationSender;
    private readonly IStoreService _storeService;
    private readonly IUserSearchService _userSearchService;
    private readonly ILogger<SendCustomerCommunicationCommandHandler> _logger;

    public SendCustomerCommunicationCommandHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepRecipientResolver recipientResolver,
        ISalesRepCommunicationResponseGroupParser responseGroupParser,
        IPushMessageService pushMessageService,
        INotificationSearchService notificationSearchService,
        INotificationSender notificationSender,
        IStoreService storeService,
        IUserSearchService userSearchService,
        ILogger<SendCustomerCommunicationCommandHandler> logger)
        : base(organizationAccessService)
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

    public virtual async Task<SalesRepCommunicationResult> Handle(SendCustomerCommunicationCommand request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        if (!await OrganizationAccessService.ServesOrganizationAsync(request.UserId, request.OrganizationId))
        {
            throw AuthorizationError.Forbidden();
        }

        var result = AbstractTypeFactory<SalesRepCommunicationResult>.TryCreateInstance();

        var caller = await GetUserAsync(request.UserId);

        var responseGroup = _responseGroupParser.GetResponseGroup(request);
        var recipients = await _recipientResolver.ResolveRecipientsAsync(request.OrganizationId, responseGroup);
        recipients = ExcludeInitiator(recipients, caller?.MemberId);

        if (recipients.Count == 0)
        {
            _logger.LogInformation("Sales Rep communication to organization {OrganizationId} has no recipients.", request.OrganizationId);
            result.Warnings.Add(ModuleConstants.Communication.Warnings.NoRecipients);
            return result;
        }

        if (request.SendPush)
        {
            await DispatchPushAsync(request, recipients, result);
        }

        if (request.SendEmail)
        {
            await DispatchEmailAsync(request, recipients, caller, result);
        }

        return result;
    }

    protected virtual void ValidateRequest(SendCustomerCommunicationCommand request)
    {
        if (string.IsNullOrEmpty(request.OrganizationId))
        {
            throw new ExecutionError("Organization is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ExecutionError("Message is required.");
        }

        if (request.Message.Length > ModuleConstants.Communication.MaxMessageLength)
        {
            throw new ExecutionError($"Message must not exceed {ModuleConstants.Communication.MaxMessageLength} characters.");
        }

        if (request.Title?.Length > ModuleConstants.Communication.MaxTitleLength)
        {
            throw new ExecutionError($"Title must not exceed {ModuleConstants.Communication.MaxTitleLength} characters.");
        }

        if (!request.SendPush && !request.SendEmail)
        {
            throw new ExecutionError("At least one delivery channel must be selected.");
        }
    }

    protected virtual async Task DispatchPushAsync(SendCustomerCommunicationCommand request, IList<Member> recipients, SalesRepCommunicationResult result)
    {
        if (await TryDispatchAsync(() => SendPushAsync(request, recipients), "push", request.OrganizationId))
        {
            result.PushSent = true;
        }
        else
        {
            result.Warnings.Add(ModuleConstants.Communication.Warnings.PushSendFailed);
        }
    }

    protected virtual async Task DispatchEmailAsync(SendCustomerCommunicationCommand request, IList<Member> recipients, ApplicationUser caller, SalesRepCommunicationResult result)
    {
        var store = await _storeService.GetByIdAsync(request.StoreId);
        if (!IsStoreAllowed(store, caller?.StoreId))
        {
            _logger.LogError(
                "Sales Rep email communication denied: user {UserId} (store {CallerStoreId}) attempted to send on store {StoreId}.",
                request.UserId, caller?.StoreId, request.StoreId);
            result.Warnings.Add(ModuleConstants.Communication.Warnings.EmailStoreAccessDenied);
            return;
        }

        if (string.IsNullOrEmpty(store.Email))
        {
            _logger.LogWarning("Sales Rep email communication skipped: store {StoreId} has no sender email configured.", request.StoreId);
            result.Warnings.Add(ModuleConstants.Communication.Warnings.EmailUnavailable);
            return;
        }

        var template = await _notificationSearchService.GetNotificationAsync<SalesRepMessageEmailNotification>(
            new TenantIdentity(request.StoreId, nameof(Store)));
        if (template == null)
        {
            _logger.LogWarning(
                "Sales Rep email communication skipped: no {Template} configured for store {StoreId}.",
                nameof(SalesRepMessageEmailNotification), request.StoreId);
            result.Warnings.Add(ModuleConstants.Communication.Warnings.EmailUnavailable);
            return;
        }

        var emailRecipients = recipients.Where(HasEmail).ToList();
        if (emailRecipients.Count == 0)
        {
            _logger.LogInformation("Sales Rep email communication to organization {OrganizationId} has no recipients with an email address.", request.OrganizationId);
            result.Warnings.Add(ModuleConstants.Communication.Warnings.EmailNoRecipients);
            return;
        }

        if (await TryDispatchAsync(() => SendEmailAsync(request, emailRecipients, store, template), "email", request.OrganizationId))
        {
            result.EmailSent = true;
        }
        else
        {
            result.Warnings.Add(ModuleConstants.Communication.Warnings.EmailSendFailed);
        }
    }

    protected static bool IsStoreAllowed(Store store, string callerStoreId)
    {
        if (store == null || string.IsNullOrEmpty(callerStoreId))
        {
            return false;
        }

        return store.Id == callerStoreId || store.TrustedGroups?.Contains(callerStoreId) == true;
    }

    private static bool HasEmail(Member member)
    {
        return member.Emails?.Any(x => !string.IsNullOrEmpty(x)) == true;
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

    protected virtual async Task<ApplicationUser> GetUserAsync(string userId)
    {
        var criteria = AbstractTypeFactory<UserSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = [userId];
        criteria.Take = 1;

        return (await _userSearchService.SearchUsersAsync(criteria)).Results.FirstOrDefault();
    }

    protected virtual IList<Member> ExcludeInitiator(IList<Member> recipients, string memberId)
    {
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

    protected virtual async Task SendEmailAsync(SendCustomerCommunicationCommand request, IList<Member> recipients, Store store, SalesRepMessageEmailNotification template)
    {
        template.From = store.Email;
        template.Title = request.Title;
        template.Message = request.Message;
        template.LanguageCode = request.CultureName;

        foreach (var recipient in recipients)
        {
            var notification = template.CloneTyped();
            notification.To = recipient.Emails.First(x => !string.IsNullOrEmpty(x));

            await _notificationSender.ScheduleSendNotificationAsync(notification);
        }
    }
}
