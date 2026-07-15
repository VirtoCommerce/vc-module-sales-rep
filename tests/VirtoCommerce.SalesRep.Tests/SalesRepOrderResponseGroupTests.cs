using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepOrderResponseGroupParser"/> — the field-selection → order response-group
/// mapping that keeps the order queries from loading data the caller didn't ask for. Guards the leaf-name matching
/// (so the connection's own <c>totalCount</c> is not mistaken for the order <c>total</c>).
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrderResponseGroupTests
{
    private static readonly ISalesRepOrderResponseGroupParser _parser = new SalesRepOrderResponseGroupParser();

    private static CustomerOrderResponseGroup Group(params string[] includeFields) =>
        EnumUtility.SafeParseFlags(_parser.GetResponseGroup(includeFields), CustomerOrderResponseGroup.Default);

    [Fact]
    public void NoFields_LoadsDefaultOnly()
    {
        Group().Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void ScalarFieldsOnly_LoadDefaultOnly()
    {
        // number/status/createdDate are scalar columns — no heavier group needed.
        Group("items.number", "items.status", "items.createdDate")
            .Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void Total_RequestsWithPrices()
    {
        // total is an object (total { amount … }); the parser must key off the "total" segment, not the leaf.
        var group = Group("items.number", "items.total.amount");
        group.Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
        group.Should().NotHaveFlag(CustomerOrderResponseGroup.WithItems);
    }

    [Fact]
    public void NestedTotalFieldOnly_StillRequestsWithPrices()
    {
        // Selecting only a field several levels under total (e.g. total { currency { code } }) still keys off the
        // "total" segment — the leaf here is "code".
        Group("items.total.currency.code").Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
    }

    [Fact]
    public void ItemsCount_RequestsWithItems()
    {
        var group = Group("items.number", "items.itemsCount");
        group.Should().HaveFlag(CustomerOrderResponseGroup.WithItems);
        group.Should().NotHaveFlag(CustomerOrderResponseGroup.WithPrices);
    }

    [Fact]
    public void TotalAndItemsCount_RequestBoth()
    {
        var group = Group("items.total.amount", "items.itemsCount");
        group.Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
        group.Should().HaveFlag(CustomerOrderResponseGroup.WithItems);
    }

    [Fact]
    public void ConnectionTotalCount_DoesNotTriggerWithPrices()
    {
        // The connection's own "totalCount" must NOT be read as the order "total" (leaf-name match, not Contains).
        Group("totalCount", "items.number").Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void LastOrderStyleLeaves_MapSameAsListPaths()
    {
        // The lastOrder resolver passes paths without the "items." prefix; the mapping must behave identically.
        Group("total.amount").Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
        Group("itemsCount").Should().HaveFlag(CustomerOrderResponseGroup.WithItems);
    }
}
