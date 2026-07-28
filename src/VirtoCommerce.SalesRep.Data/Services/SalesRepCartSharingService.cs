using System;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Teaches the XCart sharing pipeline the "Customer" wishlist scope (VCST-5332) without editing XCart: registered
// last for ICartSharingService so it wins, and delegates every non-Customer scope to the base. The XCart
// authorization handler already routes shared-wishlist access through ICartSharingService, so overriding this
// service is enough. The read checks (scope/access/IsAuthorized) are synchronous and read the target organizations
// off CartSharingSetting, which is always eager-loaded with the cart. The write path adds two guards: a structural
// one (ValidateSharingSettings, allowing Customer + a target) and an authorization one (AuthorizeSharingAsync, which
// requires the caller be a Sales Rep who serves the target organization).
public class SalesRepCartSharingService : CartSharingService
{
    private readonly ISalesRepRoleResolver _roleResolver;
    private readonly IOrganizationMembershipSearchService _membershipSearchService;

    public SalesRepCartSharingService(
        ICartAggregateRepository cartAggregateRepository,
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
        : base(cartAggregateRepository)
    {
        _roleResolver = roleResolver;
        _membershipSearchService = membershipSearchService;
    }

    public override string GetSharingScope(ShoppingCart cart)
    {
        return IsCustomerShared(cart) ? ModuleConstants.Sharing.CustomerScope : base.GetSharingScope(cart);
    }

    public override string GetSharingAccess(ShoppingCart cart, string currentUserId)
    {
        if (IsCustomerShared(cart))
        {
            // The rep (owner) keeps write; targeted customers are read-only.
            return !string.IsNullOrEmpty(currentUserId) && GetSharingOwnerUserId(cart) == currentUserId
                ? CartSharingAccess.Write
                : CartSharingAccess.Read;
        }

        return base.GetSharingAccess(cart, currentUserId);
    }

    public override bool IsAuthorized(ShoppingCart cart, string currentUserId, string currentOrganizationId)
    {
        if (IsCustomerShared(cart))
        {
            if (string.IsNullOrEmpty(currentUserId))
            {
                return false;
            }

            // The owner (rep) always sees their own list.
            if (GetSharingOwnerUserId(cart) == currentUserId)
            {
                return true;
            }

            // A targeted customer's member: their organization must be one of the Customer-scoped targets.
            // Fails closed when the caller has no organization.
            return !string.IsNullOrEmpty(currentOrganizationId)
                && cart.SharingSettings.Any(x =>
                    x.Scope == ModuleConstants.Sharing.CustomerScope
                    && x.SharedWithId == currentOrganizationId);
        }

        return base.IsAuthorized(cart, currentUserId, currentOrganizationId);
    }

    public override async Task AuthorizeSharingAsync(string scope, string sharedWithId, string currentUserId)
    {
        if (ModuleConstants.Sharing.CustomerScope.EqualsIgnoreCase(scope))
        {
            // A customer-targeted share is allowed only for a Sales Rep who actually serves the target organization
            // (same gate as sendCustomerCommunication, so "can share with an org" == "can message that org").
            if (string.IsNullOrEmpty(currentUserId)
                || string.IsNullOrEmpty(sharedWithId)
                || !await ServesOrganizationAsync(currentUserId, sharedWithId))
            {
                throw AuthorizationError.Forbidden();
            }

            return;
        }

        await base.AuthorizeSharingAsync(scope, sharedWithId, currentUserId);
    }

    protected override void ValidateSharingSettings(string scope, string sharedWithId)
    {
        if (ModuleConstants.Sharing.CustomerScope.EqualsIgnoreCase(scope))
        {
            // The Customer scope is targeted: it must name exactly one customer organization.
            if (string.IsNullOrEmpty(sharedWithId))
            {
                throw new InvalidOperationException("Customer sharing requires a target organization.");
            }

            return;
        }

        base.ValidateSharingSettings(scope, sharedWithId);
    }

    protected virtual bool IsCustomerShared(ShoppingCart cart)
    {
        return cart?.SharingSettings?.Any(x => x.Scope == ModuleConstants.Sharing.CustomerScope) == true;
    }

    // Mirrors SalesRepQueryHandlerBase.ServesOrganizationAsync: the caller must hold an unlocked membership with a
    // Sales-Rep-granting role in the target organization. Keep the two in sync.
    protected virtual async Task<bool> ServesOrganizationAsync(string userId, string organizationId)
    {
        var grantingRoleIds = await _roleResolver.GetRoleIdsGrantingAccessAsync();
        if (grantingRoleIds.Count == 0)
        {
            return false;
        }

        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserIds = [userId];
        criteria.OrganizationIds = [organizationId];
        criteria.RoleIds = grantingRoleIds.ToArray();
        criteria.OnlyUnlocked = true;

        var memberships = await _membershipSearchService.SearchAllNoCloneAsync(criteria);
        return memberships.Count > 0;
    }
}
