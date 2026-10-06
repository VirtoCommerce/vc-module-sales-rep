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
        return IsSharedWith(cart, currentOrganizationId);
    }

    public override async Task ApplyAsync(ShoppingCart cart, WishlistScopeContext context)
    {
        await AuthorizeCustomerShareAsync(context);

        // Checked BEFORE anything is written: EnsureSetting changes the scope and drops the previous scope's
        // targets, and the cart it mutates is the one held by the cached aggregate - so a write rejected after
        // that point would still be served to later reads and persisted by the next save (VCST-6113).
        // A scope change starts from nothing, the way EnsureSetting clears the targets that belonged to the
        // scope being left. "Stop sharing" is the Private scope, not an empty target set.
        var current = cart.GetEffectiveSharingSetting();
        var resulting = (Scope.EqualsIgnoreCase(current?.Scope) ? current : null)
            .GetResultingSharedWithIds(context.AddSharedWithIds, context.RemoveSharedWithIds);

        if (resulting.Count == 0)
        {
            throw new InvalidOperationException("The Customer sharing scope requires at least one target organization.");
        }

        var setting = EnsureSetting(cart, context.SharingKey);

        setting.ApplyTargets(context.AddSharedWithIds, context.RemoveSharedWithIds);
        setting.ApplyMessage(context.Message);

        SetOrganization(cart, organizationId: null);
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
        // Sorted: the member service keys its cache on the joined id list, so the same set asked for in a
        // different order would otherwise miss the cache and re-read every organization and its addresses.
        var organizations = await memberService.GetByIdsAsync(
            targets.Select(x => x.Id).Order(StringComparer.Ordinal).ToArray(),
            MemberResponseGroup.WithAddresses.ToString(),
            [nameof(Organization)]);

        // Indexed once: a list may carry a thousand targets, and a scan per target is a thousand scans of a
        // thousand rows. GroupBy rather than ToDictionary - nothing promises one row back per id.
        var organizationsById = organizations
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            if (organizationsById.TryGetValue(target.Id, out var organization))
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
