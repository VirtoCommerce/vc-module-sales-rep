using System;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A customer order projected for Sales Rep storefront views — e.g. a customer's most recent order in the
/// "My customers" list (VCST-5304). General-purpose so the same type can back any order-valued field
/// (<c>lastOrder</c>, <c>firstOrder</c>, …).
/// </summary>
public class SalesRepOrder : Entity
{
    /// <summary>Human-readable order number.</summary>
    public string Number { get; set; }

    /// <summary>Date the order was placed.</summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>Raw order status (e.g. "Cancelled").</summary>
    public string Status { get; set; }

    /// <summary>
    /// Localized label of the order's raw <see cref="Status"/>, resolved via <c>ISalesRepOrderStatusService</c>.
    /// Populated by the <c>salesRepOrders</c> handler only when requested; null otherwise (e.g. on <c>lastOrder</c>).
    /// </summary>
    public string StatusLocalized { get; set; }

    public decimal Total { get; set; }

    /// <summary>Order currency code (the currency in which the order was submitted).</summary>
    public string Currency { get; set; }

    /// <summary>Number of line items in the order.</summary>
    public int ItemsCount { get; set; }

    /// <summary>Projects a <see cref="CustomerOrder"/> onto the lightweight Sales Rep order DTO.</summary>
    public static SalesRepOrder FromOrder(CustomerOrder order)
    {
        var result = AbstractTypeFactory<SalesRepOrder>.TryCreateInstance();
        result.MapFrom(order);
        return result;
    }

    /// <summary>
    /// Populates this instance from <paramref name="order"/>. Override in a derived type
    /// (registered via <c>AbstractTypeFactory.OverrideType</c>) to map additional fields.
    /// </summary>
    protected virtual void MapFrom(CustomerOrder order)
    {
        Id = order.Id;
        Number = order.Number;
        CreatedDate = order.CreatedDate;
        Status = order.Status;
        Total = order.Total;
        Currency = order.Currency;
        ItemsCount = order.Items?.Count ?? 0;
    }
}
