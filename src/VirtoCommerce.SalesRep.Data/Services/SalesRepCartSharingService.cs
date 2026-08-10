using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Teaches the XCart sharing pipeline the "Customer" wishlist scope (VCST-5332) without editing XCart: registered last
// for ICartSharingService so it wins, delegating every non-Customer scope to base. Read checks are synchronous (they
// read the target org off the always-eager-loaded CartSharingSetting). The write path adds one authorization gate: the
// caller must be a Sales Rep who serves the target org, delegated to ISalesRepOrganizationAccessService (the same gate
// the query/communication handlers use, so "can share with an org" == "can message it").
public class SalesRepCartSharingService(
    ICartAggregateRepository cartAggregateRepository,
    ISalesRepOrganizationAccessService organizationAccessService)
    : CartSharingService(cartAggregateRepository)
{
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

    public override async Task UpdateScopeAsync(ShoppingCart cart, WishlistScopeContext context)
    {
        // Authorize the customer-targeted share before the base applies it; every other scope falls through to base.
        await AuthorizeCustomerShareAsync(context);

        await base.UpdateScopeAsync(cart, context);
    }

    protected override bool ApplyScope(ShoppingCart cart, WishlistScopeContext context)
    {
        if (ModuleConstants.Sharing.CustomerScope.EqualsIgnoreCase(context.Scope))
        {
            // The rep (owner) keeps write via GetSharingAccess; the persisted setting targets one customer
            // organization, read-only. UpdateScopeAsync has already verified the rep serves that org.
            EnsureSharingSettings(cart, context.SharingKey, ModuleConstants.Sharing.CustomerScope, CartSharingAccess.Read, context.SharedWithId);
            SetOwner(cart, context.CurrentUserId, context.CustomerName, null);
            return true;
        }

        return base.ApplyScope(cart, context);
    }

    protected virtual async Task AuthorizeCustomerShareAsync(WishlistScopeContext context)
    {
        if (!ModuleConstants.Sharing.CustomerScope.EqualsIgnoreCase(context.Scope))
        {
            return;
        }

        if (string.IsNullOrEmpty(context.CurrentUserId)
            || string.IsNullOrEmpty(context.SharedWithId)
            || !await organizationAccessService.ServesOrganizationAsync(context.CurrentUserId, context.SharedWithId))
        {
            throw AuthorizationError.Forbidden();
        }
    }

    protected virtual bool IsCustomerShared(ShoppingCart cart)
    {
        return cart?.SharingSettings?.Any(x => x.Scope == ModuleConstants.Sharing.CustomerScope) == true;
    }
}
