using System.Linq;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Teaches the XCart sharing pipeline the "Customer" wishlist scope (VCST-5332) without editing XCart: registered
// last for ICartSharingService so it wins, and delegates every non-Customer scope to the base. The XCart
// authorization handler already routes shared-wishlist access through ICartSharingService, so overriding this
// service is enough. Every check is synchronous and reads the target organizations off CartSharingSetting, which
// is always eager-loaded with the cart — so nothing scoped is injected into the (singleton) auth handler.
public class SalesRepCartSharingService : CartSharingService
{
    public SalesRepCartSharingService(ICartAggregateRepository cartAggregateRepository)
        : base(cartAggregateRepository)
    {
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

    protected virtual bool IsCustomerShared(ShoppingCart cart)
    {
        return cart?.SharingSettings?.Any(x => x.Scope == ModuleConstants.Sharing.CustomerScope) == true;
    }
}
