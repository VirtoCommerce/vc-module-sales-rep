using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Data.Services;
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

    // The repository is unused by the synchronous logic under test.
    private static SalesRepCartSharingService CreateService() => new(cartAggregateRepository: null);

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
}
