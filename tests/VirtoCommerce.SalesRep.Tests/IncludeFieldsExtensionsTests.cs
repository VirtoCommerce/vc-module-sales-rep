using FluentAssertions;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

[Trait("Category", "Unit")]
public class IncludeFieldsExtensionsTests
{
    [Theory]
    [InlineData("phone")]
    [InlineData("primaryContact.id")]
    [InlineData("items.phone")]
    [InlineData("items.primaryContact.phones")]
    [InlineData("edges.node.primaryContact.fullName")]
    public void IncludesPrimaryContact_WhenTheCustomerSelectsPhoneOrPrimaryContact(string path)
    {
        new[] { "organizationId", path }.IncludesPrimaryContact().Should().BeTrue();
    }

    [Theory]
    [InlineData("address.phone")]
    [InlineData("items.address.phone")]
    [InlineData("items.organizationName")]
    [InlineData("totalCount")]
    public void IncludesPrimaryContact_NotForOtherFields(string path)
    {
        new[] { "organizationId", path }.IncludesPrimaryContact().Should().BeFalse();
    }

    [Fact]
    public void GetPrimaryContactResponseGroup_LoadsEmailsOnlyWhenSelected()
    {
        Group("items.primaryContact.phones").Should().Be(MemberResponseGroup.WithPhones);
        Group("items.primaryContact.emails").Should().Be(MemberResponseGroup.WithPhones | MemberResponseGroup.WithEmails);
    }

    private static MemberResponseGroup Group(params string[] includeFields)
    {
        return EnumUtility.SafeParseFlags(includeFields.GetPrimaryContactResponseGroup(), MemberResponseGroup.Default);
    }
}
