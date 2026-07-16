using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepMemberResponseGroupParser"/> — the field-selection → member
/// response-group mapping shared by the customer (address / computed phone) and rep-contact (emails / phones)
/// projections. Scalars stay on Default; object/collection fields key off their segment (not the leaf); the list
/// ("items.") and single-item paths map identically; and each field fires only its own flag — the customer's
/// singular "phone" and the contact's plural "phones" are distinct segments.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepMemberResponseGroupTests
{
    private static readonly ISalesRepMemberResponseGroupParser _parser = new SalesRepMemberResponseGroupParser();

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
        // Customer scalars (organizationName/iconUrl/accountType) and contact scalars (fullName/photoUrl) — none
        // need a heavier group.
        Group("items.organizationName", "items.iconUrl", "items.accountType", "items.fullName", "items.photoUrl")
            .Should().Be(MemberResponseGroup.Default);
    }

    [Fact]
    public void Address_RequestsWithAddresses()
    {
        // address is an object (address { city … }); the parser keys off the "address" segment, not the leaf.
        var group = Group("items.organizationName", "items.address.city");
        group.Should().HaveFlag(MemberResponseGroup.WithAddresses);
        group.Should().NotHaveFlag(MemberResponseGroup.WithPhones);
        group.Should().NotHaveFlag(MemberResponseGroup.WithEmails);
    }

    [Fact]
    public void NestedAddressFieldOnly_StillRequestsWithAddresses()
    {
        Group("address.regionName").Should().HaveFlag(MemberResponseGroup.WithAddresses);
    }

    [Fact]
    public void CustomerPhone_RequestsWithPhones()
    {
        // The customer details' computed singular `phone` field.
        var group = Group("phone");
        group.Should().HaveFlag(MemberResponseGroup.WithPhones);
        group.Should().NotHaveFlag(MemberResponseGroup.WithEmails);
        group.Should().NotHaveFlag(MemberResponseGroup.WithAddresses);
    }

    [Fact]
    public void ContactPhones_RequestWithPhones()
    {
        // The rep contact's plural `phones` collection — a distinct segment from the customer's singular `phone`.
        Group("items.phones").Should().HaveFlag(MemberResponseGroup.WithPhones);
    }

    [Fact]
    public void ContactEmails_RequestWithEmails()
    {
        var group = Group("items.emails");
        group.Should().HaveFlag(MemberResponseGroup.WithEmails);
        group.Should().NotHaveFlag(MemberResponseGroup.WithAddresses);
    }

    [Fact]
    public void EmailsAndPhones_RequestBoth()
    {
        var group = Group("items.emails", "items.phones");
        group.Should().HaveFlag(MemberResponseGroup.WithEmails);
        group.Should().HaveFlag(MemberResponseGroup.WithPhones);
    }

    [Fact]
    public void ListAndSingleItemPaths_MapSame()
    {
        // The list nests fields under "items."; the single-customer query does not. Both must behave identically.
        Group("items.address.city").Should().HaveFlag(MemberResponseGroup.WithAddresses);
        Group("address.city").Should().HaveFlag(MemberResponseGroup.WithAddresses);
    }
}
