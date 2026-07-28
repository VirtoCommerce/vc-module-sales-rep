using System;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepOrder : Entity
{
    public string Number { get; set; }

    public string OrganizationId { get; set; }

    public string OrganizationName { get; set; }

    public DateTime CreatedDate { get; set; }

    public string Status { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; }

    public int ItemsCount { get; set; }

    public int ItemsQuantity { get; set; }

    public static SalesRepOrder FromOrder(CustomerOrder order)
    {
        var result = AbstractTypeFactory<SalesRepOrder>.TryCreateInstance();
        result.MapFrom(order);
        return result;
    }

    protected virtual void MapFrom(CustomerOrder order)
    {
        Id = order.Id;
        Number = order.Number;
        OrganizationId = order.OrganizationId;
        OrganizationName = order.OrganizationName;
        CreatedDate = order.CreatedDate;
        Status = order.Status;
        Total = order.Total;
        Currency = order.Currency;
        ItemsCount = order.Items?.Count ?? 0;
        ItemsQuantity = order.Items?.Sum(x => x.Quantity) ?? 0;
    }
}
