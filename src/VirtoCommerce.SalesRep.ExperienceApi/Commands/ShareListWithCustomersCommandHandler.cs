using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using MediatR;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Commands;

public class ShareListWithCustomersCommandHandler
    : SalesRepQueryHandlerBase, IRequestHandler<ShareListWithCustomersCommand, SalesRepShareListResult>
{
    private readonly ICartAggregateRepository _cartAggregateRepository;
    private readonly IStoreService _storeService;
    private readonly IMediator _mediator;

    public ShareListWithCustomersCommandHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ICartAggregateRepository cartAggregateRepository,
        IStoreService storeService,
        IMediator mediator)
        : base(roleResolver, membershipSearchService)
    {
        _cartAggregateRepository = cartAggregateRepository;
        _storeService = storeService;
        _mediator = mediator;
    }

    public virtual async Task<SalesRepShareListResult> Handle(ShareListWithCustomersCommand request, CancellationToken cancellationToken)
    {
        var organizationIds = ValidateRequest(request);

        // Data isolation: the rep may only share with organizations they actually serve.
        foreach (var organizationId in organizationIds)
        {
            if (!await ServesOrganizationAsync(request.UserId, organizationId))
            {
                throw AuthorizationError.Forbidden();
            }
        }

        var aggregate = await _cartAggregateRepository.GetCartByIdAsync(
            request.ListId, CartResponseGroup.Full.ToString(), productsIncludeFields: null, request.CultureName);
        if (aggregate?.Cart == null)
        {
            throw new ExecutionError("List not found.");
        }

        var cart = aggregate.Cart;

        // Only the owner (the rep) may publish their own list.
        if (cart.CustomerId != request.UserId)
        {
            throw AuthorizationError.Forbidden();
        }

        // Derive the stable link key before saving so the notification message can be validated up-front.
        var sharingKey = cart.SharingSettings?.FirstOrDefault()?.Id ?? Guid.NewGuid().ToString("N");
        var sharingUrl = await BuildSharingUrlAsync(request.StoreId ?? cart.StoreId, sharingKey);

        var notify = request.SendPush || request.SendEmail;
        var message = notify ? ComposeMessage(request.Message, sharingUrl) : null;
        if (notify && message.Length > ModuleConstants.Communication.MaxMessageLength)
        {
            throw new ExecutionError($"Message together with the list link must not exceed {ModuleConstants.Communication.MaxMessageLength} characters.");
        }

        ApplyCustomerSharing(cart, organizationIds, sharingKey);
        await _cartAggregateRepository.SaveAsync(aggregate);

        var result = AbstractTypeFactory<SalesRepShareListResult>.TryCreateInstance();
        result.Succeeded = true;
        result.ListId = cart.Id;
        result.SharingKey = sharingKey;
        result.SharingUrl = sharingUrl;
        result.SharedWithOrganizationIds = organizationIds;

        if (notify)
        {
            await NotifyCustomersAsync(request, organizationIds, message, result);
        }

        return result;
    }

    protected virtual IList<string> ValidateRequest(ShareListWithCustomersCommand request)
    {
        if (string.IsNullOrEmpty(request.ListId))
        {
            throw new ExecutionError("List is required.");
        }

        if (string.IsNullOrEmpty(request.StoreId))
        {
            throw new ExecutionError("Store is required.");
        }

        var organizationIds = (request.OrganizationIds ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (organizationIds.Count == 0)
        {
            throw new ExecutionError("At least one customer organization is required.");
        }

        return organizationIds;
    }

    // Rebuilds the Customer-scoped sharing settings: one per target organization, reusing the stable primary key
    // for the shared-list link. Replaces any prior sharing so removed customers lose access on save.
    protected virtual void ApplyCustomerSharing(ShoppingCart cart, IList<string> organizationIds, string sharingKey)
    {
        cart.SharingSettings ??= [];
        cart.SharingSettings.Clear();

        var isPrimary = true;
        foreach (var organizationId in organizationIds)
        {
            var setting = AbstractTypeFactory<CartSharingSetting>.TryCreateInstance();
            setting.Id = isPrimary ? sharingKey : Guid.NewGuid().ToString("N");
            setting.ShoppingCartId = cart.Id;
            setting.Scope = ModuleConstants.Sharing.CustomerScope;
            setting.Access = ModuleConstants.Sharing.CustomerAccess;
            setting.SharedWithId = organizationId;

            cart.SharingSettings.Add(setting);
            isPrimary = false;
        }
    }

    protected virtual async Task<string> BuildSharingUrlAsync(string storeId, string sharingKey)
    {
        var store = string.IsNullOrEmpty(storeId) ? null : await _storeService.GetByIdAsync(storeId);
        var baseUrl = (store?.Url ?? store?.SecureUrl)?.TrimEnd('/');

        return string.IsNullOrEmpty(baseUrl)
            ? $"/shared-list/{sharingKey}"
            : $"{baseUrl}/shared-list/{sharingKey}";
    }

    protected static string ComposeMessage(string message, string sharingUrl)
    {
        return string.IsNullOrWhiteSpace(message) ? sharingUrl : $"{message}\n\n{sharingUrl}";
    }

    // Reuses the Sales Rep communication channel (VCST-5310) so a shared list triggers the same email/push a
    // manual message would, carrying the shared-list link. Per-organization warnings bubble up to the result.
    protected virtual async Task NotifyCustomersAsync(
        ShareListWithCustomersCommand request, IList<string> organizationIds, string message, SalesRepShareListResult result)
    {
        foreach (var organizationId in organizationIds)
        {
            var command = new SendCustomerCommunicationCommand
            {
                OrganizationId = organizationId,
                SendPush = request.SendPush,
                SendEmail = request.SendEmail,
                Title = request.Title,
                Message = message,
                StoreId = request.StoreId,
                CultureName = request.CultureName,
                UserId = request.UserId,
            };

            var communicationResult = await _mediator.Send(command);

            foreach (var warning in communicationResult.Warnings)
            {
                result.Warnings.Add(warning);
            }
        }
    }
}
