using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>Response of the <c>salesRepOrderStatuses</c> query — the status tabs for the orders panel.</summary>
public class SalesRepOrderStatusesResult
{
    public IList<SalesRepOrderStatus> Items { get; set; } = [];
}
