using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerInsightsCriteria
{
    public string OrganizationId { get; set; }

    public string StoreId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string SortBy { get; set; } = ModuleConstants.Insights.Sort.Count;

    public int Take { get; set; } = ModuleConstants.Insights.DefaultTake;
}
