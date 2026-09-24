using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// The "Customer" wishlist scope (VCST-5332) as a registry policy. Reads are synchronous off the eager-loaded
// CartSharingSetting; writes add one gate - the caller must be a rep who serves the target org, delegated to
// ISalesRepOrganizationAccessService (so "can share with an org" == "can message it").
public class SalesRepCustomerCartSharingScopePolicy(ISalesRepOrganizationAccessService organizationAccessService)
    : CartSharingScopePolicyBase
{
    public override string Scope => ModuleConstants.Sharing.CustomerScope;

    public override string Description => "Customer scope (shared by a Sales Rep with specific customer organizations)";

    public override string GetAccess(ShoppingCart cart, string currentUserId)
    {
        // Owner (the rep) keeps write; targeted customers are read-only.
        return IsOwner(cart, currentUserId) ? CartSharingAccess.Write : CartSharingAccess.Read;
    }

    public override bool IsAuthorized(ShoppingCart cart, string currentUserId, string currentOrganizationId)
    {
        if (string.IsNullOrEmpty(currentUserId))
        {
            return false;
        }

        if (IsOwner(cart, currentUserId))
        {
            return true;
        }

        // A targeted customer's org must be one of the targets; fails closed when the caller has none.
        return !string.IsNullOrEmpty(currentOrganizationId)
            && cart.SharingSettings?.Any(x => x.Scope.EqualsIgnoreCase(Scope)
                && x.SharedWithId.EqualsIgnoreCase(currentOrganizationId)) == true;
    }

    public override async Task ApplyAsync(ShoppingCart cart, WishlistScopeContext context)
    {
        await AuthorizeCustomerShareAsync(context);

        // Targets one customer organization, read-only; the rep keeps write via GetAccess.
        EnsureSetting(cart, context.SharingKey, CartSharingAccess.Read, context.SharedWithId);
        SetOwner(cart, context.CurrentUserId, context.CustomerName, organizationId: null);
    }

    public override void ConfigureSearchCriteria(ShoppingCartSearchCriteria criteria)
    {
        // Owned by the rep with no owner organization (see ApplyAsync), so narrow by customer like Private.
        criteria.OrganizationId = null;
    }

    protected virtual async Task AuthorizeCustomerShareAsync(WishlistScopeContext context)
    {
        if (string.IsNullOrEmpty(context.CurrentUserId)
            || string.IsNullOrEmpty(context.SharedWithId)
            || !await organizationAccessService.ServesOrganizationAsync(context.CurrentUserId, context.SharedWithId))
        {
            throw AuthorizationError.Forbidden();
        }
    }
}
