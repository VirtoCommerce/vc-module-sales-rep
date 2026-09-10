using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.XCart.Core.Models;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Services;
using VirtoCommerce.XCart.Data.Services.SharingScopes;
using Xunit;
using Address = VirtoCommerce.CustomerModule.Core.Model.Address;

namespace VirtoCommerce.SalesRep.Tests;

// The "Customer" wishlist scope (VCST-5332; N target organizations + a persisted message since VCST-5850/5728). Most
// cases run through a real CartSharingService holding the XCart built-ins plus this policy, so the scopes are exercised
// as they compose. The IsAuthorized cases guard the data-isolation invariant: a customer sees ONLY lists shared with
// their own organization.
[Trait("Category", "Unit")]
public class SalesRepCustomerCartSharingScopePolicyTests
{
    private const string RepUserId = "rep-user-1";
    private const string OrgA = "org-a";
    private const string OrgB = "org-b";
    private const string OrgC = "org-c";
    private const string CustomerUserId = "customer-user-1";
    private const string SharingKey = "sharing-key-1";

    private static SalesRepCustomerCartSharingScopePolicy CustomerPolicy(bool servesOrganization = false, IList<Member> organizations = null) =>
        new(new FakeOrganizationAccessService(servesOrganization), new FakeMemberService(organizations ?? []));

    // XCart built-ins plus the sales-rep scope. Only the Customer policy is under test - the built-ins exercise
    // transitions, so this list need not track XCart's registration. The repository is unused by these paths.
    private static ICartSharingService SharingService(bool servesOrganization = false, IList<Member> organizations = null) =>
        new CartSharingService(
            cartAggregateRepository: null,
            [
                new PrivateCartSharingScopePolicy(),
                new AnyoneAnonymousCartSharingScopePolicy(),
                new AnyoneAuthorizedCartSharingScopePolicy(),
                new OrganizationCartSharingScopePolicy(),
                new UserCartSharingScopePolicy(),
                CustomerPolicy(servesOrganization, organizations),
            ]);

    private sealed class FakeOrganizationAccessService(bool servesOrganization) : ISalesRepOrganizationAccessService
    {
        public Task<bool> ServesOrganizationAsync(string userId, string organizationId) => Task.FromResult(servesOrganization);

        public Task<bool> ServesAllOrganizationsAsync(string userId, IList<string> organizationIds) => Task.FromResult(servesOrganization);

        public Task<IList<OrganizationMembership>> GetGrantingMembershipsAsync(IList<string> userIds = null, IList<string> organizationIds = null) => Task.FromResult<IList<OrganizationMembership>>([]);

        public Task<IList<string>> GetServedOrganizationIdsAsync(string userId) => Task.FromResult<IList<string>>([]);

        public Task<IList<string>> GetVisibleOrganizationIdsAsync(string userId, string organizationId) => Task.FromResult<IList<string>>([]);

        public Task<IList<OrganizationMembership>> GetVisibleGrantingMembershipsAsync(string userId, string organizationId) => Task.FromResult<IList<OrganizationMembership>>([]);
    }

    /// <summary>Only the read-by-ids path is real: that is all target resolution needs.</summary>
    private sealed class FakeMemberService(IList<Member> members) : IMemberService
    {
        public Task<Member[]> GetByIdsAsync(string[] memberIds, string responseGroup = null, string[] memberTypes = null) =>
            Task.FromResult(members.Where(x => memberIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToArray());

        public Task<Member> GetByIdAsync(string memberId, string responseGroup = null, string memberType = null) =>
            Task.FromResult(members.FirstOrDefault(x => x.Id == memberId));

        public Task SaveChangesAsync(Member[] members) => throw new NotSupportedException();

        public Task DeleteAsync(string[] ids, string[] memberTypes = null) => throw new NotSupportedException();
    }

    private static ShoppingCart EmptyCart() => new() { SharingSettings = [] };

    private static WishlistScopeContext ScopeContext(
        string scope,
        string currentUserId,
        IList<string> addSharedWithIds = null,
        IList<string> removeSharedWithIds = null,
        string message = null,
        string currentOrganizationId = null) =>
        new()
        {
            Scope = scope,
            SharingKey = SharingKey,
            AddSharedWithIds = addSharedWithIds,
            RemoveSharedWithIds = removeSharedWithIds,
            Message = message,
            CurrentUserId = currentUserId,
            CustomerName = "Rep Name",
            CurrentOrganizationId = currentOrganizationId,
        };

    private static WishlistScopeContext CustomerContext(IList<string> addSharedWithIds = null, IList<string> removeSharedWithIds = null, string message = null) =>
        ScopeContext(ModuleConstants.Sharing.CustomerScope, RepUserId, addSharedWithIds, removeSharedWithIds, message);

    // One setting (the sharing key) with one target row per organization - the persisted shape.
    private static ShoppingCart CustomerSharedCart(string ownerUserId, params string[] organizationIds)
    {
        return new ShoppingCart
        {
            CustomerId = ownerUserId,
            SharingSettings =
            [
                new CartSharingSetting
                {
                    Id = SharingKey,
                    Scope = ModuleConstants.Sharing.CustomerScope,
                    Access = CartSharingAccess.Read,
                    Targets = organizationIds.Select(organizationId => new CartSharingSettingTarget
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        CartSharingSettingId = SharingKey,
                        SharedWithId = organizationId,
                    }).ToList(),
                },
            ],
        };
    }

    private static IEnumerable<string> TargetIds(ShoppingCart cart) => cart.SharingSettings.Single().Targets.Select(x => x.SharedWithId);

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
    public void IsAuthorized_MemberOfAnySharedOrganization_ReturnsTrue()
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
    public async Task IsAuthorized_RemovedOrganization_IsDeniedImmediately()
    {
        // Revocation is a target row gone, not a flag - the next authorization check already fails closed.
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        await service.UpdateScopeAsync(cart, CustomerContext(removeSharedWithIds: [OrgB]));

        service.IsAuthorized(cart, CustomerUserId, OrgB).Should().BeFalse();
        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().BeTrue();
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
    public async Task UpdateScopeAsync_CustomerScope_RepServesOrganizations_WritesOneSettingWithAllTargets()
    {
        var service = SharingService(servesOrganization: true);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, CustomerContext(addSharedWithIds: [OrgA, OrgB], message: "Have a look"));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(SharingKey);
        setting.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        setting.Access.Should().Be(CartSharingAccess.Read);
        setting.Message.Should().Be("Have a look");
        TargetIds(cart).Should().BeEquivalentTo(OrgA, OrgB);
        cart.CustomerId.Should().Be(RepUserId); // owner stays the rep
        cart.OrganizationId.Should().BeNull(); // no owner organization: the list is reachable by key, not by org listing
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_AddingAnOrganization_DoesNotRevokeTheOthers()
    {
        // VCST-5707: sharing with a second customer used to replace the first one.
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA);

        await service.UpdateScopeAsync(cart, CustomerContext(addSharedWithIds: [OrgB]));

        cart.SharingSettings.Should().ContainSingle().Which.Id.Should().Be(SharingKey); // the existing link keeps working
        TargetIds(cart).Should().BeEquivalentTo(OrgA, OrgB);
        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().BeTrue();
        service.IsAuthorized(cart, CustomerUserId, OrgB).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_RepDoesNotServeAnAddedOrganization_ThrowsForbidden()
    {
        var service = SharingService(servesOrganization: false);
        var cart = EmptyCart();

        // DATA-ISOLATION INVARIANT: the server-side gate - a rep must not publish to an org they do not serve.
        await service.Invoking(x => x.UpdateScopeAsync(cart, CustomerContext(addSharedWithIds: [OrgC])))
            .Should().ThrowAsync<AuthorizationError>();
        cart.SharingSettings.Should().BeEmpty(); // nothing persisted
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_RemovingAnOrganization_DoesNotRequireServingIt()
    {
        // VCST-5925 §2.5: a rep unassigned from an organization must still be able to revoke its access.
        var service = SharingService(servesOrganization: false);
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);

        await service.UpdateScopeAsync(cart, CustomerContext(removeSharedWithIds: [OrgB]));

        TargetIds(cart).Should().Equal(OrgA);
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_Anonymous_ThrowsForbidden()
    {
        var service = SharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext(ModuleConstants.Sharing.CustomerScope, currentUserId: null, addSharedWithIds: [OrgA])))
            .Should().ThrowAsync<AuthorizationError>();
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_NoTargetLeft_Throws()
    {
        // "Stop sharing" is the Private scope; a Customer list without a target is rejected rather than saved empty.
        var service = SharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), CustomerContext()))
            .Should().ThrowAsync<InvalidOperationException>();
        await service.Invoking(x => x.UpdateScopeAsync(CustomerSharedCart(RepUserId, OrgA), CustomerContext(removeSharedWithIds: [OrgA])))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateScopeAsync_CustomerScope_Message_IsSavedKeptAndCleared()
    {
        // VCST-5728: the message is persisted with the share; null keeps it, an empty string clears it.
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA);

        await service.UpdateScopeAsync(cart, CustomerContext(message: "Check these out"));
        cart.SharingSettings.Single().Message.Should().Be("Check these out");

        await service.UpdateScopeAsync(cart, CustomerContext(addSharedWithIds: [OrgB]));
        cart.SharingSettings.Single().Message.Should().Be("Check these out");

        await service.UpdateScopeAsync(cart, CustomerContext(message: ""));
        cart.SharingSettings.Single().Message.Should().BeNull();
    }

    [Fact]
    public async Task UpdateScopeAsync_UnregisteredScope_Throws()
    {
        // A scope no registered policy claims is rejected loudly.
        var service = SharingService(servesOrganization: true);

        await service.Invoking(x => x.UpdateScopeAsync(EmptyCart(), ScopeContext("BogusScope", RepUserId)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateScopeAsync_OrganizationScope_IsUnaffectedByTheCustomerPolicy()
    {
        // A built-in scope keeps its own write path; the serves-org gate is irrelevant to it.
        var service = SharingService(servesOrganization: false);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(CartSharingScope.Organization, RepUserId, currentOrganizationId: OrgA));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Scope.Should().Be(CartSharingScope.Organization);
        setting.Targets.Should().BeNullOrEmpty();
        cart.OrganizationId.Should().Be(OrgA);
    }

    [Fact]
    public async Task UpdateScopeAsync_NullScope_IsNoOp()
    {
        // A null scope (e.g. a rename-only edit) neither authorizes nor writes.
        var service = SharingService(servesOrganization: false);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(scope: null, RepUserId));

        cart.SharingSettings.Should().BeEmpty();
    }

    // Edits of an ALREADY-shared list: the sharing key (the /shared-list/{key} link) survives every transition,
    // and customer targets never outlive the Customer scope.

    [Theory]
    [InlineData(CartSharingScope.Private)]
    [InlineData(CartSharingScope.Organization)]
    [InlineData(CartSharingScope.AnyoneAnonymous)]
    public async Task UpdateScopeAsync_LeavingCustomerScope_ClearsTargetsAndMessageAndKeepsSharingKey(string scope)
    {
        // DATA-ISOLATION INVARIANT: stale targets must not survive, or re-sharing could expose the list again.
        var service = SharingService(servesOrganization: true);
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB);
        cart.SharingSettings.Single().Message = "Check these out";

        await service.UpdateScopeAsync(cart, ScopeContext(scope, RepUserId, currentOrganizationId: OrgC));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(SharingKey);
        setting.Scope.Should().Be(scope);
        setting.Targets.Should().BeEmpty();
        setting.Message.Should().BeNull();
        service.IsAuthorized(cart, CustomerUserId, OrgA).Should().Be(scope == CartSharingScope.AnyoneAnonymous);
    }

    [Fact]
    public async Task UpdateScopeAsync_NullScope_PreservesExistingCustomerShare()
    {
        // A rename-only edit submits no scope; dropping the share here would silently unshare the list.
        var service = SharingService(servesOrganization: false);
        var cart = CustomerSharedCart(RepUserId, OrgA);

        await service.UpdateScopeAsync(cart, ScopeContext(scope: null, RepUserId, addSharedWithIds: [OrgB]));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Id.Should().Be(SharingKey);
        setting.Scope.Should().Be(ModuleConstants.Sharing.CustomerScope);
        TargetIds(cart).Should().Equal(OrgA); // OrgB was not added either
    }

    [Fact]
    public async Task UpdateScopeAsync_BuiltInScope_IgnoresStrayTargets()
    {
        // Targets are meaningful only for the Customer scope; dropped, not rejected, so wishlists stay saveable.
        var service = SharingService(servesOrganization: true);
        var cart = EmptyCart();

        await service.UpdateScopeAsync(cart, ScopeContext(CartSharingScope.Organization, RepUserId, addSharedWithIds: [OrgA], currentOrganizationId: OrgB));

        var setting = cart.SharingSettings.Should().ContainSingle().Subject;
        setting.Scope.Should().Be(CartSharingScope.Organization);
        setting.Targets.Should().BeNullOrEmpty();
        cart.OrganizationId.Should().Be(OrgB); // owner org comes from the caller's context, not from the stray target
    }

    [Fact]
    public async Task ResolveTargetsAsync_ResolvesOrganizationDisplayData_IndependentOfTheRepAssignment()
    {
        // VCST-5925 §1.2: names resolve for every grant on the list (even for an org the rep no longer serves);
        // a deleted organization keeps its id and has no name.
        var organizations = new List<Member>
        {
            new Organization
            {
                Id = OrgA,
                Name = "Acme Corp",
                IconUrl = "/acme.png",
                Addresses = [new Address { City = "Berlin", RegionName = "Berlin", IsDefault = true }],
            },
            new Organization { Id = OrgB, Name = "No Address Ltd" },
        };
        var service = SharingService(servesOrganization: false, organizations);
        var cart = CustomerSharedCart(RepUserId, OrgA, OrgB, OrgC);

        var targets = await service.ResolveTargetsAsync(cart.SharingSettings.Single());

        targets.Select(x => x.Id).Should().Equal(OrgA, OrgB, OrgC);
        targets[0].Name.Should().Be("Acme Corp");
        targets[0].Subtitle.Should().Be("Berlin, Berlin");
        targets[0].ImageUrl.Should().Be("/acme.png");
        targets[1].Name.Should().Be("No Address Ltd");
        targets[1].Subtitle.Should().BeNull();
        targets[2].Name.Should().BeNull();
    }
}
