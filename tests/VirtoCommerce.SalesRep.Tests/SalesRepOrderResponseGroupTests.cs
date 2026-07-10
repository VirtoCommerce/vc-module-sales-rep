using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepOrder.GetResponseGroup"/> — the field-selection → order response-group
/// mapping that keeps the order queries from loading data the caller didn't ask for. Guards the leaf-name matching
/// (so the connection's own <c>totalCount</c> is not mistaken for the order <c>total</c>).
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepOrderResponseGroupTests
{
    private static CustomerOrderResponseGroup Group(params string[] includeFields) =>
        EnumUtility.SafeParseFlags(SalesRepOrder.GetResponseGroup(includeFields), CustomerOrderResponseGroup.Default);

    [Fact]
    public void NoFields_LoadsDefaultOnly()
    {
        Group().Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void ScalarFieldsOnly_LoadDefaultOnly()
    {
        // number/status/currency/createdDate are scalar columns — no heavier group needed.
        Group("items.number", "items.status", "items.currency", "items.createdDate")
            .Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void Total_RequestsWithPrices()
    {
        var group = Group("items.number", "items.total");
        group.Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
        group.Should().NotHaveFlag(CustomerOrderResponseGroup.WithItems);
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
        var group = Group("items.total", "items.itemsCount");
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
        // The lastOrder resolver passes bare leaf names (no "items." prefix); the mapping must behave identically.
        Group("total").Should().HaveFlag(CustomerOrderResponseGroup.WithPrices);
        Group("itemsCount").Should().HaveFlag(CustomerOrderResponseGroup.WithItems);
    }
}
