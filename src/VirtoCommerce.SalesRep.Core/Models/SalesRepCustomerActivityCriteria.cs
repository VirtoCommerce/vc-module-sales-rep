using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerActivityCriteria
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
