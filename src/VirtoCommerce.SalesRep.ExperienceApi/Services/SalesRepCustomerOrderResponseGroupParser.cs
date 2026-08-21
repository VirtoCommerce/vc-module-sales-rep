using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Maps the fields selected on X-Order's CustomerOrderType to the order response group that loads exactly
/// those, so a list showing a handful of columns no longer pulls the whole order graph. Takes the selected
/// paths as the connection reports them (see <see cref="GetOrderField"/>).
/// </summary>
public class SalesRepCustomerOrderResponseGroupParser : ISalesRepCustomerOrderResponseGroupParser
{
    private const string StatusDisplayValueField = nameof(CustomerOrder.Status) + "DisplayValue";
    private const string CouponsField = "coupons";
    private const string AvailablePaymentMethodsField = "availablePaymentMethods";

    private const string ItemsPrefix = "items.";
    private const string EdgeNodePrefix = "edges.node.";

    // The page's own fields, which say nothing about how much of an order to load. Anything else at this level
    // is kept and looked up like an order field, so an unknown one still falls back to Full.
    private static readonly HashSet<string> _connectionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "items",
        "edges",
        "cursor",
        "pageInfo",
        "totalCount",
        "term_facets",
        "range_facets",
        "filter_facets",
    };

    // IMPORTANT: a field missing from here means "load the full graph" — that is the safe answer, and it is the
    // same answer the Full entries below get. Adding a field is a claim that a narrower load still answers it
    // correctly; check that claim against OrderRepository.GetCustomerOrdersByIdsAsync (which flags gate a
    // query), CustomerOrder.ReduceDetails (which values a missing flag blanks) and CustomerOrderService
    // .ProcessModel (which recalculates the derived money, and only when the group is *exactly* Full).
    private static readonly Dictionary<string, CustomerOrderResponseGroup> _responseGroupByField = new(StringComparer.OrdinalIgnoreCase)
    {
        // The order row and the values stored on it — loaded by every response group.
        [nameof(CustomerOrder.Id)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.OperationType)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ParentOperationId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.Number)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.IsApproved)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.Status)] = CustomerOrderResponseGroup.Default,
        [StatusDisplayValueField] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.Comment)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.OuterId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.IsCancelled)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CancelledDate)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CancelReason)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ObjectType)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CustomerId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CustomerName)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ChannelId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.StoreId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.StoreName)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.OrganizationId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.OrganizationName)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.EmployeeId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.EmployeeName)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ShoppingCartId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.IsPrototype)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.SubscriptionId)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.SubscriptionNumber)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.PurchaseOrderNumber)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.TaxType)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.LanguageCode)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CreatedDate)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.CreatedBy)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ModifiedDate)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.ModifiedBy)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.Currency)] = CustomerOrderResponseGroup.Default,
        [nameof(CustomerOrder.TaxDetails)] = CustomerOrderResponseGroup.Default,

        // Resolved without touching the order graph: coupons searches promotion usages by order id, and the
        // available payment methods come from the store the order was placed in.
        [CouponsField] = CustomerOrderResponseGroup.Default,
        [AvailablePaymentMethodsField] = CustomerOrderResponseGroup.Default,

        // Stored on the order row too, but blanked by ReduceDetails unless the group asks for prices.
        [nameof(CustomerOrder.Total)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.SubTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.SubTotalWithTax)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.ShippingTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.ShippingTotalWithTax)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.PaymentTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.PaymentTotalWithTax)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.DiscountAmount)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.DiscountTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.DiscountTotalWithTax)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.TaxTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.TaxPercentRate)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.Fee)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.FeeWithTax)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.FeeTotal)] = CustomerOrderResponseGroup.WithPrices,
        [nameof(CustomerOrder.FeeTotalWithTax)] = CustomerOrderResponseGroup.WithPrices,

        // Own tables, one query each.
        [nameof(CustomerOrder.Addresses)] = CustomerOrderResponseGroup.WithAddresses,
        [nameof(CustomerOrder.Discounts)] = CustomerOrderResponseGroup.WithDiscounts,
        [nameof(CustomerOrder.DynamicProperties)] = CustomerOrderResponseGroup.WithDynamicProperties,

        // Stored nowhere: DefaultCustomerOrderTotalsCalculator derives these from the line items, shipments and
        // payments, and CustomerOrderService runs it for the exactly-Full group only. A narrower group answers
        // them with zeros, so they have to ask for the whole graph.
        [nameof(CustomerOrder.SubTotalDiscount)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.SubTotalDiscountWithTax)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.SubTotalTaxTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.ShippingSubTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.ShippingSubTotalWithTax)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.ShippingDiscountTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.ShippingDiscountTotalWithTax)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.ShippingTaxTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.PaymentSubTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.PaymentSubTotalWithTax)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.PaymentDiscountTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.PaymentDiscountTotalWithTax)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.PaymentTaxTotal)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.OrderTotals)] = CustomerOrderResponseGroup.Full,

        // The same reason one level down: the line item, shipment and payment types expose their own derived
        // money (ExtendedPrice, PlacedPrice, ListTotal, Total, Sum), which again only exactly-Full fills in.
        [nameof(CustomerOrder.Items)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.Shipments)] = CustomerOrderResponseGroup.Full,
        [nameof(CustomerOrder.InPayments)] = CustomerOrderResponseGroup.Full,
    };

    public virtual string GetResponseGroup(IList<string> includeFields)
    {
        var result = CustomerOrderResponseGroup.Default;

        foreach (var field in GetSelectedFields(includeFields))
        {
            var fieldResponseGroup = GetFieldResponseGroup(field);
            if (fieldResponseGroup == CustomerOrderResponseGroup.Full)
            {
                return CustomerOrderResponseGroup.Full.ToString();
            }

            result |= fieldResponseGroup;
        }

        return result.ToString();
    }

    protected virtual CustomerOrderResponseGroup GetFieldResponseGroup(string field)
    {
        return _responseGroupByField.TryGetValue(field, out var responseGroup)
            ? responseGroup
            : CustomerOrderResponseGroup.Full;
    }

    /// <summary>
    /// The order fields themselves, taken from the head of each selected path: "total { formattedAmount }"
    /// arrives as "total.formattedAmount" and is a claim on "total", not on "formattedAmount".
    /// </summary>
    protected virtual IEnumerable<string> GetSelectedFields(IList<string> includeFields)
    {
        return (includeFields ?? [])
            .Select(GetOrderField)
            .Where(x => x != null)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The order field a selected path names, or null for a path that describes the page instead. Paths arrive
    /// as the connection sees them, and a connection exposes its page both flattened ("items") and Relay-style
    /// ("edges { node }") — so an order's own items arrive as "items.items.sku", and reading that head
    /// literally would put every list back on the full graph.
    /// </summary>
    protected virtual string GetOrderField(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (path.StartsWith(ItemsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[ItemsPrefix.Length..];
        }
        else if (path.StartsWith(EdgeNodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[EdgeNodePrefix.Length..];
        }
        else if (_connectionFields.Contains(path.Split('.')[0]))
        {
            return null;
        }

        return path.Split('.')[0];
    }
}
