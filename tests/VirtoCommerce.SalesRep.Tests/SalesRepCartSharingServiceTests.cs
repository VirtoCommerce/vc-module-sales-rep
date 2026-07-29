using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Models;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepCartSharingService"/> — the "Customer" wishlist scope (VCST-5332).
/// The synchronous scope/access/authorization methods read the target organizations off the cart's SharingSettings,
/// so no database or aggregate repository is needed (the repository is only used by GetWishlistBySharingKeyAsync).
/// The <c>IsAuthorized</c> cases guard the data-isolation invariant: a customer must see ONLY lists shared with their
/// own organization.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepCartSharingServiceTests
{
    private const string RepUserId = "rep-user-1";
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";
    private const string OrgC = "org-c";
    private const string CustomerUserId = "customer-user-1";

    // The repository and the write-path collaborators are unused by the synchronous read logic under test.
    private static SalesRepCartSharingService CreateService() => new(cartAggregateRepository: null, roleResolver: null, membershipSearchService: null);

    // Stubs the async "does this rep serve the org" lookup so the write-path (UpdateScopeAsync) can be tested without a DB.
    private sealed class TestSharingService(bool servesOrganization) : SalesRepCartSharingService(null, null, null)
    {
        protected override Task<bool> ServesOrganizationAsync(string userId, string organizationId) => Task.FromResult(servesOrganization);
    }

    private static ShoppingCart EmptyCart() => new() { SharingSettings = new List<CartSharingSetting>() };

    private static WishlistScopeContext ScopeContext(string scope, string sharedWithId, string currentUserId, string currentOrganizationId = null) =>
        new()
        {
            Scope = scope,
            SharingKey = "sharing-key-1",
            SharedWithId = sharedWithId,
            CurrentUserId = currentUserId,
            CustomerName = "Rep Name",
            CurrentOrganizationId = currentOrganizationId,
        };

    private static ShoppingCart CustomerSharedCart(string ownerUserId, params string[] organizationIds)
    {
        return new ShoppingCart
        {
            CustomerId = ownerUserId,
            SharingSettings = organizationIds.Select(organizationId => new CartSharingSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                Scope = ModuleConstants.Sharing.CustomerScope,
                Access = ModuleConstants.Sharing.CustomerAccess,
                SharedWithId = organizationId,
            }).ToList<CartSharingSetting>(),
        };
    }

    [Fact]
    public void GetSharingScope_CustomerSetting_ReturnsCustomer()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.GetSharingScope(cart).Should().Be(ModuleConstants.Sharing.CustomerScope);
    }

    [Fact]
    public void GetSharingScope_NoCustomerSetting_DelegatesToBase()
    {
        var service = CreateService();

        // No sharing settings and no owner organization → base default is Private.
        service.GetSharingScope(new ShoppingCart()).Should().Be(CartSharingScope.Private);
        // No sharing settings but an owner organization → base treats it as Organization.
        service.GetSharingScope(new ShoppingCart { OrganizationId = OrgA }).Should().Be(CartSharingScope.Organization);
    }

    [Fact]
    public void IsAuthorized_Owner_ReturnsTrue()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        // The rep who owns the list always sees it, regardless of the organization claim.
        service.IsAuthorized(cart, RepUserId, currentOrganizationId: null).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_MemberOfSharedOrganization_ReturnsTrue()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().BeTrue();
        service.IsAuthorized(cart, CustomerUserId, OrgB).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_MemberOfUnsharedOrganization_ReturnsFalse()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        // DATA-ISOLATION INVARIANT: a member of an organization the list was NOT shared with must be denied.
        service.IsAuthorized(cart, CustomerUserId, OrgC).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_Anonymous_ReturnsFalse()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.IsAuthorized(cart, currentUserId: null, currentOrganizationId: OrgA).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_AuthenticatedMemberWithoutOrganization_ReturnsFalse()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.IsAuthorized(cart, CustomerUserId, currentOrganizationId: null).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_NonCustomerScope_DelegatesToBase()
    {
        var service = CreateService();

        // Organization-scoped list → base authorizes members of the owner organization only.
        var cart = new ShoppingCart
        {
            CustomerId = RepUserId,
            OrganizationId = OrgA,
            SharingSettings = [new CartSharingSetting { Scope = CartSharingScope.Organization }],
        };

        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().BeTrue();
        service.IsAuthorized(cart, CustomerUserId, OrgC).Should().BeFalse();
    }

    [Fact]
    public void GetSharingAccess_Owner_ReturnsWrite_TargetedCustomer_ReturnsRead()
    {
        var service = CreateService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.GetSharingAccess(cart, RepUserId).Should().Be(CartSharingAccess.Write);
        service.GetSharingAccess(cart, CustomerUserId).Should().Be(CartSharingAccess.Read);
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_RepServesOrganization_WritesCustomerSetting()
    {
        var service = new TestSharingService(servesOrganization: true);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgA, RepUserId));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        setting.SharedWithId.Should().Be(OrgA);
        setting.Access.Should().Be(CartSharingAccess.Read);
        cart.CustomerId.Should().Be(RepUserId); // owner stays the rep
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_RepDoesNotServeOrganization_ThrowsForbidden()
    {
        var service = new TestSharingService(servesOrganization: false);
        var cart = EmptyCart();

        // DATA-ISOLATION INVARIANT: a caller must not publish to an organization they do not serve, even if they
        // otherwise hold the Sales Rep role. This is the server-side gate for F1 (frontend gating is not enough).
        await service.Invoking(x => x.UpdateScopeAsync(cart, ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgC, RepUserId)))
            .Should().ThrowAsync<AuthorizationError>();
        cart.SharingSettings.Should().BeEmpty(); // nothing persisted
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_AnonymousOrNoTarget_ThrowsForbidden()
    {
        // Even when the org would be served, an unauthenticated caller or a missing target is denied (fails closed).
        var service = new TestSharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgA, currentUserId: null)))
            .Should().ThrowAsync<AuthorizationError>();
        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext(ModuleConstants.Sharing.CustomerScope, sharedWithId: null, RepUserId)))
            .Should().ThrowAsync<AuthorizationError>();
    }

    [Fact]
    public async Task UpdateScopeAsync_UnsupportedScope_Throws()
    {
        // A scope neither the base pipeline nor sales-rep recognizes is rejected loudly (ApplyScope returns false).
        var service = new TestSharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext("BogusScope", sharedWithId: null, RepUserId)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateScopeAsync_OrganizationScope_DelegatesToBase()
    {
        // Non-Customer scope: sales-rep defers to the base ApplyScope (serves-org is irrelevant here).
        var service = new TestSharingService(servesOrganization: false);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(CartSharingScope.Organization, sharedWithId: null, RepUserId, currentOrganizationId: OrgA));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Scope.Should().Be(CartSharingScope.Organization);
        setting.SharedWithId.Should().BeNull();
        cart.OrganizationId.Should().Be(OrgA);
    }

    [Fact]
    public async Task UpdateScopeAsync_NullScope_IsNoOp()
    {
        // A null scope (e.g. a rename-only edit) neither authorizes nor writes.
        var service = new TestSharingService(servesOrganization: false);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(scope: null, sharedWithId: null, RepUserId));

        cart.SharingSettings.Should().BeEmpty();
    }
}
