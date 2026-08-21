using FluentAssertions;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests;

/// <summary>
/// Pure-logic tests for <see cref="SalesRepCustomerOrderResponseGroupParser"/> — the CustomerOrderType field
/// selection → order response-group mapping behind salesRepCustomerOrders. The mapping trades round trips for a
/// correctness risk in one direction only, so the cases below pin both halves: a list selection must come out
/// narrow, and anything that a narrowed load would answer with zeros (or not at all) must come out Full.
/// </summary>
[Trait("Category", "Unit")]
public class SalesRepCustomerOrderResponseGroupTests
{
    private static readonly ISalesRepCustomerOrderResponseGroupParser _parser = new SalesRepCustomerOrderResponseGroupParser();

    private static CustomerOrderResponseGroup Group(params string[] includeFields) =>
        EnumUtility.SafeParseFlags(_parser.GetResponseGroup(includeFields), CustomerOrderResponseGroup.Default);

    [Fact]
    public void NoFields_LoadDefaultOnly()
    {
        Group().Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void ScalarFieldsOnly_LoadDefaultOnly()
    {
        Group("number", "status", "statusDisplayValue", "createdDate", "organizationName")
            .Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void StorefrontListSelection_LoadsPricesAndNothingElse()
    {
        // The columns the hub's customer-orders page actually asks for, in the shape the connection reports them.
        // This is the case the whole mapping exists for: previously Full, so every page walked the line items,
        // payments, shipments and dynamic properties of ten orders to print nine scalars and a total.
        var group = Group(
            "items.id", "items.number", "items.organizationId", "items.organizationName", "items.customerId",
            "items.createdDate", "items.status", "items.statusDisplayValue", "items.total.formattedAmount",
            "totalCount", "term_facets.terms.term");

        group.Should().Be(CustomerOrderResponseGroup.WithPrices);
    }

    [Fact]
    public void NestedPath_KeysOffTheOrderField()
    {
        // "total { formattedAmount }" is a claim on total, not on formattedAmount.
        Group("total.formattedAmount").Should().Be(CustomerOrderResponseGroup.WithPrices);
        Group("currency.code").Should().Be(CustomerOrderResponseGroup.Default);
    }

    // A connection exposes its page both flattened and Relay-style, and both wrap the order. The word "items"
    // therefore means the page in one position and the order's line items in the other.
    [Theory]
    [InlineData("items.total.formattedAmount", CustomerOrderResponseGroup.WithPrices)]
    [InlineData("edges.node.total.formattedAmount", CustomerOrderResponseGroup.WithPrices)]
    [InlineData("items.items.sku", CustomerOrderResponseGroup.Full)]
    [InlineData("edges.node.items.sku", CustomerOrderResponseGroup.Full)]
    public void ConnectionWrapper_IsNotTheOrderField(string includeField, CustomerOrderResponseGroup expected)
    {
        Group(includeField).Should().Be(expected);
    }

    [Fact]
    public void PageFields_DoNotLoadAnything()
    {
        Group("totalCount", "cursor", "pageInfo.hasNextPage", "term_facets.terms.term", "range_facets.name")
            .Should().Be(CustomerOrderResponseGroup.Default);
    }

    [Fact]
    public void UnknownConnectionField_FallsBackToFull()
    {
        // A field added to the connection rather than to the order is not a page field we know to be free.
        Group("items.number", "someConnectionExtension.value").Should().Be(CustomerOrderResponseGroup.Full);
    }

    [Theory]
    [InlineData("addresses.line1", CustomerOrderResponseGroup.WithAddresses)]
    [InlineData("discounts.amount", CustomerOrderResponseGroup.WithDiscounts)]
    [InlineData("dynamicProperties.name", CustomerOrderResponseGroup.WithDynamicProperties)]
    public void OwnTableField_RequestsItsOwnFlag(string includeField, CustomerOrderResponseGroup expected)
    {
        Group(includeField).Should().Be(expected);
    }

    [Fact]
    public void SeveralFields_CombineTheirFlags()
    {
        Group("number", "total.formattedAmount", "addresses.line1")
            .Should().Be(CustomerOrderResponseGroup.WithPrices | CustomerOrderResponseGroup.WithAddresses);
    }

    [Theory]
    [InlineData("items.sku")]
    [InlineData("shipments.trackingNumber")]
    [InlineData("inPayments.gatewayCode")]
    public void OrderGraphField_RequiresFull(string includeField)
    {
        // Line items, shipments and payments expose money that CustomerOrderService derives only for the
        // exactly-Full group (ExtendedPrice, PlacedPrice, ListTotal, Total, Sum), so they cannot be narrowed.
        Group(includeField).Should().Be(CustomerOrderResponseGroup.Full);
    }

    [Theory]
    [InlineData("subTotalDiscount.amount")]
    [InlineData("subTotalTaxTotal.amount")]
    [InlineData("shippingSubTotal.amount")]
    [InlineData("shippingDiscountTotal.amount")]
    [InlineData("paymentSubTotal.amount")]
    [InlineData("paymentTaxTotal.amount")]
    [InlineData("orderTotals.total")]
    public void DerivedOrderMoney_RequiresFull(string includeField)
    {
        // These are not stored on the order row — the totals calculator fills them in, and only for exactly Full.
        // A narrower group would answer them with a well-formed zero, which is the one failure mode worth this
        // whole list of cases.
        Group(includeField).Should().Be(CustomerOrderResponseGroup.Full);
    }

    [Fact]
    public void UnknownField_FallsBackToFull()
    {
        // A field another module added to CustomerOrderType can read anything; over-fetching is the safe answer.
        Group("number", "someExtensionField.value").Should().Be(CustomerOrderResponseGroup.Full);
    }

    [Fact]
    public void FieldNameCasing_IsIgnored()
    {
        Group("Total.Amount", "ORGANIZATIONNAME").Should().Be(CustomerOrderResponseGroup.WithPrices);
    }
}
