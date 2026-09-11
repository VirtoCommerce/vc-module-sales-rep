using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Extensions;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// The "Customer" wishlist scope (VCST-5332): one list, N customer organizations as targets, one message. Reads are
// synchronous off the eager-loaded setting. Adds are gated by "the caller is a rep who serves the org" (so "can share
// with an org" == "can message it"); removals are not - the owner must always be able to revoke.
public class SalesRepCustomerCartSharingScopePolicy(ISalesRepOrganizationAccessService organizationAccessService, IMemberService memberService)
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
        var setting = cart.GetEffectiveSharingSetting();

        return !string.IsNullOrEmpty(currentOrganizationId)
            && setting?.Scope.EqualsIgnoreCase(Scope) == true
            && setting.Targets?.Any(x => x.SharedWithId.EqualsIgnoreCase(currentOrganizationId)) == true;
    }

    public override async Task ApplyAsync(ShoppingCart cart, WishlistScopeContext context)
    {
        await AuthorizeCustomerShareAsync(context);

        var setting = EnsureSetting(cart, context.SharingKey, CartSharingAccess.Read);

        setting.ApplyTargets(context.AddSharedWithIds, context.RemoveSharedWithIds);
        setting.ApplyMessage(context.Message);

        // "Stop sharing" is the Private scope, not an empty target set.
        if (setting.Targets.IsNullOrEmpty())
        {
            throw new InvalidOperationException("The Customer sharing scope requires at least one target organization.");
        }

        SetOwner(cart, context.CurrentUserId, context.CustomerName, organizationId: null);
    }

    public override async Task<IList<WishlistSharingTarget>> ResolveTargetsAsync(IList<string> sharedWithIds)
    {
        // One call for every list in the request: the batch loader hands over the ids of the whole page at once.
        var targets = await base.ResolveTargetsAsync(sharedWithIds);

        if (targets.Count == 0)
        {
            return targets;
        }

        // Resolved for whoever may read the setting, regardless of the rep's current assignment: a grant to an
        // organization the rep no longer serves is the one they most need to recognize before revoking it.
        var organizations = await memberService.GetByIdsAsync(
            targets.Select(x => x.Id).ToArray(),
            MemberResponseGroup.WithAddresses.ToString(),
            [nameof(Organization)]);

        foreach (var target in targets)
        {
            var organization = organizations.FirstOrDefault(x => x.Id.EqualsIgnoreCase(target.Id));

            if (organization != null)
            {
                target.Name = organization.Name;
                target.Subtitle = GetSubtitle(organization);
                target.ImageUrl = organization.IconUrl;
            }
        }

        return targets;
    }

    public override void ConfigureSearchCriteria(ShoppingCartSearchCriteria criteria)
    {
        // Owned by the rep with no owner organization (see ApplyAsync), so narrow by customer like Private.
        criteria.OrganizationId = null;
    }

    protected virtual async Task AuthorizeCustomerShareAsync(WishlistScopeContext context)
    {
        if (string.IsNullOrEmpty(context.CurrentUserId))
        {
            throw AuthorizationError.Forbidden();
        }

        var addedIds = (context.AddSharedWithIds ?? []).Where(x => !string.IsNullOrEmpty(x)).ToList();

        if (addedIds.Count > 0 && !await organizationAccessService.ServesAllOrganizationsAsync(context.CurrentUserId, addedIds))
        {
            throw AuthorizationError.Forbidden();
        }
    }

    protected virtual string GetSubtitle(Member organization)
    {
        var address = organization.Addresses?.FirstOrDefault(x => x.IsDefault) ?? organization.Addresses?.FirstOrDefault();
        var subtitle = string.Join(", ", new[] { address?.City, address?.RegionName }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrEmpty(subtitle) ? null : subtitle;
    }
}
