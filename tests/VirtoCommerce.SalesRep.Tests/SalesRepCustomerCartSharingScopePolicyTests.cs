using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Services;
using VirtoCommerce.XCart.Data.Services.SharingScopes;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

// The "Customer" wishlist scope (VCST-5332). Most cases run through a real CartSharingService holding the XCart
// built-ins plus this policy, so the scopes are exercised as they compose. The IsAuthorized cases guard the
// data-isolation invariant: a customer sees ONLY lists shared with their own organization.
[Trait("Category", "Unit")]
public class SalesRepCustomerCartSharingScopePolicyTests
{
    private const string RepUserId = "rep-user-1";
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";
    private const string OrgC = "org-c";
    private const string CustomerUserId = "customer-user-1";

    private static SalesRepCustomerCartSharingScopePolicy CustomerPolicy(bool servesOrganization = false) =>
        new(new FakeOrganizationAccessService(servesOrganization));

    // XCart built-ins plus the sales-rep scope. Only the Customer policy is under test - the built-ins exercise
    // transitions, so this list need not track XCart's registration. The repository is unused by these paths.
    private static ICartSharingService SharingService(bool servesOrganization = false) =>
        new CartSharingService(
            cartAggregateRepository: null,
            [
                new PrivateCartSharingScopePolicy(),
                new AnyoneAnonymousCartSharingScopePolicy(),
                new AnyoneAuthorizedCartSharingScopePolicy(),
                new OrganizationCartSharingScopePolicy(),
                new UserCartSharingScopePolicy(),
                CustomerPolicy(servesOrganization),
            ]);

    private sealed class FakeOrganizationAccessService(bool servesOrganization) : ISalesRepOrganizationAccessService
    {
        public Task<bool> ServesOrganizationAsync(string userId, string organizationId) => Task.FromResult(servesOrganization);

        public Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null) => Task.FromResult<IList<OrganizationMembership>>([]);

        public Task<IList<string>> GetServedOrganizationIdsAsync(string userId) => Task.FromResult<IList<string>>([]);

        public Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId) => Task.FromResult<IList<string>>([]);

        public Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId) => Task.FromResult<IList<OrganizationMembership>>([]);
    }

    private static ShoppingCart EmptyCart() => new() { SharingSettings = [] };

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
                Access = CartSharingAccess.Read,
                SharedWithId = organizationId,
            }).ToList(),
        };
    }

    [Fact]
    public void Registration_ExposesTheCustomerScope()
    {
        var policy = CustomerPolicy();

        policy.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        policy.CanApply.Should().BeTrue();
        policy.Description.Should().NotBeNullOrWhiteSpace(); // becomes the GraphQL WishlistScopeType enum description
    }

    [Fact]
    public void GetSharingScope_CustomerSetting_ReturnsCustomer()
    {
        var cart = CustomerSharedCart(RepUserId, OrgA);

        SharingService().GetSharingScope(cart).Should().Be(ModuleConstants.Sharing.CustomerScope);
    }

    [Fact]
    public void GetSharingScope_NoCustomerSetting_FallsBackToTheBuiltInScopes()
    {
        var service = SharingService();

        // No sharing settings and no owner organization → Private.
        service.GetSharingScope(new ShoppingCart()).Should().Be(CartSharingScope.Private);
        // No sharing settings but an owner organization → Organization.
        service.GetSharingScope(new ShoppingCart { OrganizationId = OrgA }).Should().Be(CartSharingScope.Organization);
    }

    [Fact]
    public void IsAuthorized_Owner_ReturnsTrue()
    {
        var cart = CustomerSharedCart(RepUserId, OrgA);

        // The owning rep always sees it, whatever the organization claim.
        SharingService().IsAuthorized(cart, RepUserId, currentOrganizationId: null).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_MemberOfSharedOrganization_ReturnsTrue()
    {
        var service = SharingService();
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().BeTrue();
        service.IsAuthorized(cart, CustomerUserId, OrgB).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_MemberOfUnsharedOrganization_ReturnsFalse()
    {
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        // DATA-ISOLATION INVARIANT: a member of an organization the list was NOT shared with must be denied.
        SharingService().IsAuthorized(cart, CustomerUserId, OrgC).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_AnonymousOrMemberWithoutOrganization_ReturnsFalse()
    {
        var service = SharingService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.IsAuthorized(cart, currentUserId: null, currentOrganizationId: OrgA).Should().BeFalse();
        service.IsAuthorized(cart, CustomerUserId, currentOrganizationId: null).Should().BeFalse();
    }

    [Fact]
    public void IsAuthorized_IdsDifferingOnlyByCase_StillMatch()
    {
        // The dispatcher selects case-insensitively, so both the target match here and XCart's owner check must too.
        var cart = CustomerSharedCart(RepUserId, OrgA);
        var service = SharingService();

        service.IsAuthorized(cart, CustomerUserId, OrgA.ToUpperInvariant()).Should().BeTrue();
        service.IsAuthorized(cart, RepUserId.ToUpperInvariant(), currentOrganizationId: null).Should().BeTrue();
    }

    [Fact]
    public void IsAuthorized_BuiltInScope_IsUnaffectedByTheCustomerPolicy()
    {
        var service = SharingService();

        // Organization-scoped list → members of the owner organization only.
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
        var service = SharingService();
        var cart = CustomerSharedCart(RepUserId, OrgA);

        service.GetSharingAccess(cart, RepUserId).Should().Be(CartSharingAccess.Write);
        service.GetSharingAccess(cart, CustomerUserId).Should().Be(CartSharingAccess.Read);
    }

    [Fact]
    public void ConfigureSearchCriteria_NarrowsToTheOwningRep()
    {
        // Stored with no owner organization, so listing them must not filter by one.
        var criteria = new ShoppingCartSearchCriteria { CustomerId = RepUserId, OrganizationId = OrgA };

        SharingService().ConfigureSearchCriteria(criteria, ModuleConstants.Sharing.CustomerScope);

        criteria.CustomerId.Should().Be(RepUserId);
        criteria.OrganizationId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_RepServesOrganization_WritesCustomerSetting()
    {
        var service = SharingService(servesOrganization: true);
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
        var service = SharingService(servesOrganization: false);
        var cart = EmptyCart();

        // DATA-ISOLATION INVARIANT: the server-side gate - a rep must not publish to an org they do not serve.
        await service.Invoking(x => x.UpdateScopeAsync(cart, ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgC, RepUserId)))
            .Should().ThrowAsync<AuthorizationError>();
        cart.SharingSettings.Should().BeEmpty(); // nothing persisted
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_AnonymousOrNoTarget_ThrowsForbidden()
    {
        // Even when the org would be served, an unauthenticated caller or a missing target is denied (fails closed).
        var service = SharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgA, currentUserId: null)))
            .Should().ThrowAsync<AuthorizationError>();
        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext(ModuleConstants.Sharing.CustomerScope, sharedWithId: null, RepUserId)))
            .Should().ThrowAsync<AuthorizationError>();
    }

    [Fact]
    public async Task UpdateScopeAsync_UnregisteredScope_Throws()
    {
        // A scope no registered policy claims is rejected loudly.
        var service = SharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext("BogusScope", sharedWithId: null, RepUserId)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateScopeAsync_OrganizationScope_IsUnaffectedByTheCustomerPolicy()
    {
        // A built-in scope keeps its own write path; the serves-org gate is irrelevant to it.
        var service = SharingService(servesOrganization: false);
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
        var service = SharingService(servesOrganization: false);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(scope: null, sharedWithId: null, RepUserId));

        cart.SharingSettings.Should().BeEmpty();
    }

    // Edits of an ALREADY-shared list: the sharing key (the /shared-list/{key} link) survives every transition,
    // and a customer target never outlives the Customer scope.

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_Retarget_KeepsSharingKeyAndReplacesTarget()
    {
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA);
        var originalKey = cart.SharingSettings.Single().Id;

        await service.UpdateScopeAsync(cart, ScopeContext(ModuleConstants.Sharing.CustomerScope, OrgB, RepUserId));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(originalKey); // the existing link keeps working...
        setting.SharedWithId.Should().Be(OrgB); // ...and now resolves for the new customer only
        setting.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        setting.Access.Should().Be(CartSharingAccess.Read);
    }

    [Theory]
    [InlineData(CartSharingScope.Private)]
    [InlineData(CartSharingScope.Organization)]
    [InlineData(CartSharingScope.AnyoneAnonymous)]
    public async Task UpdateScopeAsync_LeavingCustomerScope_ClearsTargetAndKeepsSharingKey(string scope)
    {
        // DATA-ISOLATION INVARIANT: a stale target must not survive, or re-sharing could expose the list again.
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA);
        var originalKey = cart.SharingSettings.Single().Id;

        await service.UpdateScopeAsync(cart, ScopeContext(scope, sharedWithId: null, RepUserId, currentOrganizationId: OrgB));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(originalKey);
        setting.Scope.Should().Be(scope);
        setting.SharedWithId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateScopeAsync_NullScope_PreservesExistingCustomerShare()
    {
        // A rename-only edit submits no scope; dropping the share here would silently unshare the list.
        var service = SharingService(servesOrganization: false);
        var cart = CustomerSharedCart(RepUserId, OrgA);
        var originalKey = cart.SharingSettings.Single().Id;

        await service.UpdateScopeAsync(cart, ScopeContext(scope: null, sharedWithId: OrgB, RepUserId));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(originalKey);
        setting.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        setting.SharedWithId.Should().Be(OrgA); // not retargeted to OrgB either
    }

    [Fact]
    public async Task UpdateScopeAsync_BuiltInScope_IgnoresStraySharedWithId()
    {
        // A target is meaningful only for the Customer scope; dropped, not rejected, so wishlists stay saveable.
        var service = SharingService(servesOrganization: true);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(CartSharingScope.Organization, OrgA, RepUserId, currentOrganizationId: OrgB));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Scope.Should().Be(CartSharingScope.Organization);
        setting.SharedWithId.Should().BeNull();
        cart.OrganizationId.Should().Be(OrgB); // owner org comes from the caller's context, not from the stray target
    }
}
