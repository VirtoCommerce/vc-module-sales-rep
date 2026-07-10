using System;
using System.Collections.Generic;
using System.Linq;
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

    public string Status { get; set; }

    public decimal Total { get; set; }

    /// <summary>Order currency code (the currency in which the order was submitted).</summary>
    public string Currency { get; set; }

    /// <summary>Number of line items in the order.</summary>
    public int ItemsCount { get; set; }

    /// <summary>
    /// Builds the minimal <see cref="CustomerOrderResponseGroup"/> needed to populate only the fields the caller
    /// actually requested, from the GraphQL selection paths (see <c>AstFieldExtensions.GetAllNodesPaths</c>). The
    /// scalar columns (<see cref="Number"/>/<see cref="Status"/>/<see cref="Currency"/>/<see cref="CreatedDate"/>)
    /// come with <see cref="CustomerOrderResponseGroup.Default"/>, so only <see cref="Total"/> (needs
    /// <see cref="CustomerOrderResponseGroup.WithPrices"/> — the order pipeline zeroes prices for lighter groups)
    /// and <see cref="ItemsCount"/> (needs the line items loaded) opt into a heavier group. Shared by both order
    /// surfaces (the <c>salesRepOrders</c> list and the <c>lastOrder</c> field) so they can't drift.
    /// </summary>
    public static string GetResponseGroup(IEnumerable<string> includeFields)
    {
        var result = CustomerOrderResponseGroup.Default;

        // Match on the leaf field name so the connection's own "totalCount" isn't mistaken for the order "total".
        var leafFields = (includeFields ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => x.Split('.')[^1])
            .ToArray();

        if (leafFields.Contains(nameof(Total), StringComparer.OrdinalIgnoreCase))
        {
            result |= CustomerOrderResponseGroup.WithPrices;
        }

        if (leafFields.Contains(nameof(ItemsCount), StringComparer.OrdinalIgnoreCase))
        {
            result |= CustomerOrderResponseGroup.WithItems;
        }

        return result.ToString();
    }

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
