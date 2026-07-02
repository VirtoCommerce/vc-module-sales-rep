using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// A customer's most recent order, shown in the Sales Rep "My customers" list (VCST-5304).
/// </summary>
public class SalesRepLastOrder
{
    public string Id { get; set; }

    /// <summary>Human-readable order number.</summary>
    public string Number { get; set; }

    /// <summary>Date the order was placed.</summary>
    public DateTime CreatedDate { get; set; }

    public string Status { get; set; }

    public decimal Total { get; set; }

    /// <summary>Order currency code.</summary>
    public string Currency { get; set; }
}
