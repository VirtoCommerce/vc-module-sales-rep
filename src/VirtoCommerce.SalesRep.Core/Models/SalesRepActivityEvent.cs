using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivityEvent
{
    public string Category { get; set; }

    public string Type { get; set; }

    public DateTime OccurredAt { get; set; }

    public string Precision { get; set; }

    public int Count { get; set; } = 1;

    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public string OrderId { get; set; }

    public string OrderNumber { get; set; }

    public string OrderStatus { get; set; }

    public decimal? OrderTotal { get; set; }

    public string OrderCurrency { get; set; }

    public string SearchTerm { get; set; }

    public string ProductId { get; set; }

    public string ProductCode { get; set; }

    public string ProductName { get; set; }

    public string ProductSlug { get; set; }

    public string ProductImageUrl { get; set; }
}
