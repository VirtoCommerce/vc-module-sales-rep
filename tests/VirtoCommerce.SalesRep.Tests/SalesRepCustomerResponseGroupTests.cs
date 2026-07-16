using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepCustomerResponseGroupParser"/> — the field-selection → member
/// response-group mapping that keeps the customer queries from loading collections (addresses, phones) the caller
/// didn't ask for. Scalars (iconUrl, organizationName, accountType) must stay on Default; object fields must key
/// off their segment, not the leaf; and the list ("items." prefix) and details paths must map identically.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepCustomerResponseGroupTests
{
    private static readonly ISalesRepCustomerResponseGroupParser _parser = new SalesRepCustomerResponseGroupParser();

    private static MemberResponseGroup Group(params string[] includeFields) =>
        EnumUtility.SafeParseFlags(_parser.GetResponseGroup(includeFields), MemberResponseGroup.Full);

    [Fact]
    public void NoFields_LoadsDefaultOnly()
    {
        Group().Should().Be(MemberResponseGroup.Default);
    }

    [Fact]
    public void ScalarFieldsOnly_LoadDefaultOnly()
    {
        // organizationId/organizationName/iconUrl/accountType are scalar columns — no heavier group needed.
        Group("items.organizationId", "items.organizationName", "items.iconUrl", "items.accountType")
            .Should().Be(MemberResponseGroup.Default);
    }

    [Fact]
    public void Address_RequestsWithAddresses()
    {
        // address is an object (address { city … }); the parser must key off the "address" segment, not the leaf.
        var group = Group("items.organizationName", "items.address.city");
        group.Should().HaveFlag(MemberResponseGroup.WithAddresses);
        group.Should().NotHaveFlag(MemberResponseGroup.WithPhones);
    }

    [Fact]
    public void NestedAddressFieldOnly_StillRequestsWithAddresses()
    {
        // Selecting only a field under address still keys off the "address" segment — the leaf here is "regionName".
        Group("address.regionName").Should().HaveFlag(MemberResponseGroup.WithAddresses);
    }

    [Fact]
    public void Phone_RequestsWithPhones()
    {
        var group = Group("phone");
        group.Should().HaveFlag(MemberResponseGroup.WithPhones);
        group.Should().NotHaveFlag(MemberResponseGroup.WithAddresses);
    }

    [Fact]
    public void AddressAndPhone_RequestBoth()
    {
        var group = Group("address.city", "phone");
        group.Should().HaveFlag(MemberResponseGroup.WithAddresses);
        group.Should().HaveFlag(MemberResponseGroup.WithPhones);
    }

    [Fact]
    public void ListAndDetailsPaths_MapSame()
    {
        // The list nests fields under "items."; the single-customer query does not. Both must behave identically.
        Group("items.address.city").Should().HaveFlag(MemberResponseGroup.WithAddresses);
        Group("address.city").Should().HaveFlag(MemberResponseGroup.WithAddresses);
    }
}
