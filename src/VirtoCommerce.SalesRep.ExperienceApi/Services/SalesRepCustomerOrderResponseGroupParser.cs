using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCustomerOrderResponseGroupParser : ISalesRepCustomerOrderResponseGroupParser
{
    private const string StatusDisplayValueField = nameof(CustomerOrder.Status) + "DisplayValue";
    private const string CouponsField = "coupons";
    private const string AvailablePaymentMethodsField = "availablePaymentMethods";

    private const string ItemsPrefix = "items.";
    private const string EdgeNodePrefix = "edges.node.";
    private const string MetaFieldPrefix = "__";

    // The page's own fields. Anything else here is looked up as an order field, so it falls back to Full.
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

    // IMPORTANT: a missing field means "load the full graph" — the safe answer. Adding one claims a narrower
    // load still answers it; check that against OrderRepository.GetCustomerOrdersByIdsAsync,
    // CustomerOrder.ReduceDetails, CustomerOrderService.ProcessModel and the field's own CustomerOrderType
    // resolver. A field needing a heavier flag returns Full outright rather than accumulating, so a narrowed
    // group is never exactly Full — its money is the stored columns, never the recomputed ones.
    private static readonly Dictionary<string, CustomerOrderResponseGroup> _responseGroupByField = new(StringComparer.OrdinalIgnoreCase)
    {
        // Stored on the order row, loaded by every response group.
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

        // Resolved without touching the order graph.
        [CouponsField] = CustomerOrderResponseGroup.Default,
        [AvailablePaymentMethodsField] = CustomerOrderResponseGroup.Default,

        // Stored too, but blanked by ReduceDetails without WithPrices.
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

        // Stored nowhere: DefaultCustomerOrderTotalsCalculator derives these, and only for exactly-Full.
        // A narrower group answers them with zeros.
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

        // Same reason one level down: their types expose derived money (ExtendedPrice, ListTotal, Sum, ...).
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

    protected virtual IEnumerable<string> GetSelectedFields(IList<string> includeFields)
    {
        return (includeFields ?? [])
            .Select(GetOrderField)
            .Where(x => x != null);
    }

    // The order field a path names, or null when it names the page. A connection wraps the node both flattened
    // and Relay-style, so an order's own items arrive as "items.items.sku" — reading that head literally would
    // put every list back on the full graph.
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
        else if (_connectionFields.Contains(Head(path)))
        {
            return null;
        }

        var field = Head(path);

        // Apollo injects __typename into every selection set; treating it as an unknown field would send every
        // real request to Full. Checked after the wrapper comes off — it arrives at both levels.
        return field.StartsWith(MetaFieldPrefix, StringComparison.Ordinal) ? null : field;
    }

    private static string Head(string path)
    {
        var dot = path.IndexOf('.');

        return dot < 0 ? path : path[..dot];
    }
}
